using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TwitchSubtitles.Web.Models;
using TwitchSubtitles.Web.Services;

namespace TwitchSubtitles.Web.Handlers;

public class SubtitlesWebSocketHandler
{
    private readonly IMlServiceClient _mlClient;
    private readonly SettingsService _settingsService;
    private readonly WebSocketBroadcaster _broadcaster;
    private readonly ILogger<SubtitlesWebSocketHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    private string _lastRusText = "";
    private string _lastEngText = "";

    public SubtitlesWebSocketHandler(
        IMlServiceClient mlClient,
        SettingsService settingsService,
        WebSocketBroadcaster broadcaster,
        ILogger<SubtitlesWebSocketHandler> logger)
    {
        _mlClient = mlClient;
        _settingsService = settingsService;
        _broadcaster = broadcaster;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        };
    }

    public async Task HandleAsync(WebSocket webSocket)
    {
        var clientId = _broadcaster.AddClient(webSocket);
        _logger.LogInformation("[WS] Client {ClientId} connected", clientId);

        try
        {
            var buffer = new byte[1024 * 4];

            while (webSocket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;

                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        _logger.LogInformation("[WS] Client {ClientId} requested close", clientId);
                        await webSocket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "Closing",
                            CancellationToken.None);
                        return;
                    }

                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var messageJson = Encoding.UTF8.GetString(ms.ToArray());
                    await ProcessMessageAsync(messageJson, webSocket);
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "[WS] Client {ClientId} WebSocket exception", clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WS] Client {ClientId} unexpected error", clientId);
            await SendErrorAsync(webSocket, "Internal server error");
        }
        finally
        {
            _broadcaster.RemoveClient(clientId);
            _logger.LogInformation("[WS] Client {ClientId} disconnected", clientId);
        }
    }

    private async Task ProcessMessageAsync(string messageJson, WebSocket webSocket)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageJson);
            var typeStr = doc.RootElement.TryGetProperty("type", out var typeProp)
                ? typeProp.GetString()
                : null;

            switch (typeStr)
            {
                case "audio_chunk":
                    await ProcessAudioChunkAsync(messageJson, webSocket);
                    break;

                default:
                    _logger.LogWarning("[WS] Unknown message type: {Type}", typeStr);
                    await SendErrorAsync(webSocket, $"Unknown message type: {typeStr}");
                    break;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[WS] JSON parsing error");
            await SendErrorAsync(webSocket, "Invalid JSON");
        }
    }

    private async Task ProcessAudioChunkAsync(string messageJson, WebSocket webSocket)
    {
        try
        {
            _logger.LogInformation("[ML] Sending chunk to ML service");

            var chunkMessage = JsonSerializer.Deserialize<AudioChunkMessage>(messageJson, _jsonOptions);
            if (chunkMessage == null || string.IsNullOrEmpty(chunkMessage.AudioData))
            {
                await SendErrorAsync(webSocket, "Empty audio data");
                return;
            }

            var audioBytes = Convert.FromBase64String(chunkMessage.AudioData);
            var mlResponse = await _mlClient.ProcessAudioChunkAsync(audioBytes);

            var settings = _settingsService.GetSettings();
            var rusText = mlResponse.RusText;
            var engText = mlResponse.EngText;

            if (settings.DedupEnabled && settings.OverlapMs > 0)
            {
                rusText = Deduplicate(_lastRusText, rusText);
                engText = Deduplicate(_lastEngText, engText);
            }

            _lastRusText = mlResponse.RusText;
            _lastEngText = mlResponse.EngText;

            var subtitleMessage = new SubtitleMessage
            {
                Type = MessageTypeId.Subtitle,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                RusText = rusText,
                EngText = engText
            };

            await _broadcaster.BroadcastAsync(subtitleMessage);

            _logger.LogInformation("[WS] Broadcast subtitle: {RusText} / {EngText}", rusText, engText);
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "[ML] Invalid base64 in audio data");
            await SendErrorAsync(webSocket, "Invalid audio data format");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ML] Failed to process audio chunk");
            await SendErrorAsync(webSocket, "Failed to process audio");
        }
    }

    private static string Deduplicate(string previous, string current)
    {
        if (string.IsNullOrEmpty(previous) || string.IsNullOrEmpty(current))
            return current;

        var prevOriginal = previous.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currOriginal = current.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (prevOriginal.Length == 0 || currOriginal.Length == 0)
            return current;

        var prevNorm = prevOriginal.Select(NormalizeWord).ToArray();
        var currNorm = currOriginal.Select(NormalizeWord).ToArray();

        var maxOverlap = 0;
        for (var i = 1; i <= Math.Min(prevNorm.Length, currNorm.Length); i++)
        {
            var match = true;
            for (var j = 0; j < i; j++)
            {
                if (prevNorm[prevNorm.Length - i + j] != currNorm[j])
                {
                    match = false;
                    break;
                }
            }

            if (match) maxOverlap = i;
        }

        if (maxOverlap < 2)
            return current;

        if (maxOverlap >= currOriginal.Length)
            return "";

        return string.Join(' ', currOriginal[maxOverlap..]);
    }

    private static string NormalizeWord(string word)
    {
        return new string(word.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
    }

    private async Task SendErrorAsync(WebSocket webSocket, string errorText)
    {
        if (webSocket.State != WebSocketState.Open)
            return;

        var errorMessage = new ErrorMessage
        {
            Type = MessageTypeId.Error,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Error = errorText
        };

        var json = JsonSerializer.Serialize(errorMessage, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);

        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None);
    }
}
