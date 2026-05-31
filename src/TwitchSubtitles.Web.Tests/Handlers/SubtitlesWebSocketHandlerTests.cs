using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Moq;
using TwitchSubtitles.Web.Handlers;
using TwitchSubtitles.Web.Models;
using TwitchSubtitles.Web.Services;

namespace TwitchSubtitles.Web.Tests.Handlers;

/// <summary>
/// Тесты для SubtitlesWebSocketHandler.
/// </summary>
public class SubtitlesWebSocketHandlerTests
{
    private readonly Mock<IMlServiceClient> _mlClientMock;
    private readonly Mock<ILogger<SubtitlesWebSocketHandler>> _loggerMock;
    private readonly SettingsService _settingsService;
    private readonly WebSocketBroadcaster _broadcaster;
    private readonly SubtitlesWebSocketHandler _handler;

    private static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public SubtitlesWebSocketHandlerTests()
    {
        _mlClientMock = new Mock<IMlServiceClient>();
        _loggerMock = new Mock<ILogger<SubtitlesWebSocketHandler>>();
        _settingsService = new SettingsService(Path.Combine(Path.GetTempPath(), $"ws_test_{Guid.NewGuid()}.json"));
        _broadcaster = new WebSocketBroadcaster(Mock.Of<ILogger<WebSocketBroadcaster>>());
        _handler = new SubtitlesWebSocketHandler(_mlClientMock.Object, _settingsService, _broadcaster, _loggerMock.Object);
    }

    [Fact]
    public async Task HandleAsync_ValidAudioChunk_ReturnsSubtitles()
    {
        // Arrange
        var ws = new TestWebSocket();
        var audioData = Convert.ToBase64String(new byte[] { 0x01, 0x02, 0x03 });

        ws.EnqueueText(JsonSerializer.Serialize(new AudioChunkMessage
        {
            Type = MessageTypeId.AudioChunk,
            AudioData = audioData,
            Timestamp = 1000
        }, SnakeCase));

        ws.EnqueueClose();

        _mlClientMock
            .Setup(x => x.ProcessAudioChunkAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new SubtitlesResponse { RusText = "Привет", EngText = "Hello" });

        // Act
        await _handler.HandleAsync(ws);

        // Assert
        _mlClientMock.Verify(x => x.ProcessAudioChunkAsync(It.IsAny<byte[]>()), Times.Once);
        Assert.Single(ws.SentMessages);

        var response = JsonSerializer.Deserialize<SubtitleMessage>(ws.SentMessages[0], SnakeCase);
        Assert.NotNull(response);
        Assert.Equal(MessageTypeId.Subtitle, response.Type);
        Assert.Equal("Привет", response.RusText);
        Assert.Equal("Hello", response.EngText);
    }

    [Fact]
    public async Task HandleAsync_EmptyAudioData_ReturnsError()
    {
        // Arrange
        var ws = new TestWebSocket();

        ws.EnqueueText(JsonSerializer.Serialize(new AudioChunkMessage
        {
            Type = MessageTypeId.AudioChunk,
            AudioData = "",
            Timestamp = 1000
        }, SnakeCase));

        ws.EnqueueClose();

        // Act
        await _handler.HandleAsync(ws);

        // Assert
        _mlClientMock.Verify(x => x.ProcessAudioChunkAsync(It.IsAny<byte[]>()), Times.Never);
        Assert.Single(ws.SentMessages);

        var response = JsonSerializer.Deserialize<ErrorMessage>(ws.SentMessages[0], SnakeCase);
        Assert.NotNull(response);
        Assert.Equal(MessageTypeId.Error, response.Type);
        Assert.Equal("Empty audio data", response.Error);
    }

    [Fact]
    public async Task HandleAsync_InvalidJson_ReturnsError()
    {
        // Arrange
        var ws = new TestWebSocket();
        ws.EnqueueText("not valid json!!!");
        ws.EnqueueClose();

        // Act
        await _handler.HandleAsync(ws);

        // Assert
        Assert.Single(ws.SentMessages);

        var response = JsonSerializer.Deserialize<ErrorMessage>(ws.SentMessages[0], SnakeCase);
        Assert.NotNull(response);
        Assert.Equal(MessageTypeId.Error, response.Type);
        Assert.Equal("Invalid JSON", response.Error);
    }

    [Fact]
    public async Task HandleAsync_MlServiceThrows_ReturnsError()
    {
        // Arrange
        var ws = new TestWebSocket();
        var audioData = Convert.ToBase64String(new byte[] { 0x01 });

        ws.EnqueueText(JsonSerializer.Serialize(new AudioChunkMessage
        {
            Type = MessageTypeId.AudioChunk,
            AudioData = audioData,
            Timestamp = 1000
        }, SnakeCase));

        ws.EnqueueClose();

        _mlClientMock
            .Setup(x => x.ProcessAudioChunkAsync(It.IsAny<byte[]>()))
            .ThrowsAsync(new InvalidOperationException("ML down"));

        // Act
        await _handler.HandleAsync(ws);

        // Assert
        Assert.Single(ws.SentMessages);

        var response = JsonSerializer.Deserialize<ErrorMessage>(ws.SentMessages[0], SnakeCase);
        Assert.NotNull(response);
        Assert.Equal(MessageTypeId.Error, response.Type);
        Assert.Equal("Failed to process audio", response.Error);
    }

    [Fact]
    public async Task HandleAsync_InvalidBase64_ReturnsError()
    {
        // Arrange
        var ws = new TestWebSocket();

        ws.EnqueueText(JsonSerializer.Serialize(new AudioChunkMessage
        {
            Type = MessageTypeId.AudioChunk,
            AudioData = "!!!not-base64!!!",
            Timestamp = 1000
        }, SnakeCase));

        ws.EnqueueClose();

        // Act
        await _handler.HandleAsync(ws);

        // Assert
        Assert.Single(ws.SentMessages);

        var response = JsonSerializer.Deserialize<ErrorMessage>(ws.SentMessages[0], SnakeCase);
        Assert.NotNull(response);
        Assert.Equal(MessageTypeId.Error, response.Type);
        Assert.Equal("Invalid audio data format", response.Error);
    }

    [Fact]
    public async Task HandleAsync_CloseMessage_ClosesGracefully()
    {
        // Arrange
        var ws = new TestWebSocket();
        ws.EnqueueClose();

        // Act
        await _handler.HandleAsync(ws);

        // Assert
        Assert.Empty(ws.SentMessages);
        Assert.True(ws.CloseCalled);
    }

    [Fact]
    public async Task HandleAsync_MultipleChunks_ProcessesAll()
    {
        // Arrange
        var ws = new TestWebSocket();
        var audioData = Convert.ToBase64String(new byte[] { 0x01 });

        for (int i = 0; i < 3; i++)
        {
            ws.EnqueueText(JsonSerializer.Serialize(new AudioChunkMessage
            {
                Type = MessageTypeId.AudioChunk,
                AudioData = audioData,
                Timestamp = i * 1000
            }, SnakeCase));
        }

        ws.EnqueueClose();

        _mlClientMock
            .Setup(x => x.ProcessAudioChunkAsync(It.IsAny<byte[]>()))
            .ReturnsAsync(new SubtitlesResponse { RusText = "Текст", EngText = "Text" });

        // Act
        await _handler.HandleAsync(ws);

        // Assert
        _mlClientMock.Verify(x => x.ProcessAudioChunkAsync(It.IsAny<byte[]>()), Times.Exactly(3));
        Assert.Equal(3, ws.SentMessages.Count);
    }
}

/// <summary>
/// Testable WebSocket implementation that simulates receiving and sending messages.
/// </summary>
internal class TestWebSocket : WebSocket
{
    private readonly Queue<(WebSocketMessageType Type, byte[] Data)> _receiveQueue = new();
    private bool _closed;

    public List<string> SentMessages { get; } = new();
    public bool CloseCalled { get; private set; }

    public override WebSocketCloseStatus? CloseStatus => _closed ? WebSocketCloseStatus.NormalClosure : null;
    public override string CloseStatusDescription => string.Empty;
    public override string SubProtocol => string.Empty;
    public override WebSocketState State => _closed ? WebSocketState.Closed : WebSocketState.Open;

    public void EnqueueText(string message)
    {
        _receiveQueue.Enqueue((WebSocketMessageType.Text, Encoding.UTF8.GetBytes(message)));
    }

    public void EnqueueClose()
    {
        _receiveQueue.Enqueue((WebSocketMessageType.Close, []));
    }

    public override void Abort() => _closed = true;

    public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
    {
        _closed = true;
        CloseCalled = true;
        return Task.CompletedTask;
    }

    public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken)
        => Task.CompletedTask;

    public override void Dispose() => _closed = true;

    public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
    {
        if (_receiveQueue.Count == 0)
        {
            var tcs = new TaskCompletionSource<WebSocketReceiveResult>();
            tcs.SetException(new WebSocketException("No more messages"));
            return tcs.Task;
        }

        var (type, data) = _receiveQueue.Dequeue();

        if (type == WebSocketMessageType.Close)
        {
            _closed = true;
            return Task.FromResult(new WebSocketReceiveResult(0, WebSocketMessageType.Close, true));
        }

        var count = Math.Min(data.Length, buffer.Count);
        Array.Copy(data, 0, buffer.Array!, buffer.Offset, count);
        return Task.FromResult(new WebSocketReceiveResult(count, WebSocketMessageType.Text, endOfMessage: true));
    }

    public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
    {
        if (!_closed)
        {
            SentMessages.Add(Encoding.UTF8.GetString(buffer.Array!, buffer.Offset, buffer.Count));
        }
        return Task.CompletedTask;
    }
}
