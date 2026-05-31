using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TwitchSubtitles.Web.Models;

namespace TwitchSubtitles.Web.Services;

public class WebSocketBroadcaster
{
    private readonly ConcurrentDictionary<string, WebSocket> _clients = new();
    private readonly ILogger<WebSocketBroadcaster> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public WebSocketBroadcaster(ILogger<WebSocketBroadcaster> logger)
    {
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
        };
    }

    public string AddClient(WebSocket webSocket)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        _clients[id] = webSocket;
        _logger.LogInformation("[Broadcast] Client {Id} connected. Total: {Count}", id, _clients.Count);
        return id;
    }

    public void RemoveClient(string id)
    {
        _clients.TryRemove(id, out _);
        _logger.LogInformation("[Broadcast] Client {Id} disconnected. Total: {Count}", id, _clients.Count);
    }

    public async Task BroadcastAsync(SubtitleMessage message)
    {
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(bytes);

        var deadClients = new List<string>();

        foreach (var (id, ws) in _clients)
        {
            if (ws.State != WebSocketState.Open)
            {
                deadClients.Add(id);
                continue;
            }

            try
            {
                await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch (WebSocketException)
            {
                deadClients.Add(id);
            }
        }

        foreach (var id in deadClients)
        {
            _clients.TryRemove(id, out _);
        }
    }
}
