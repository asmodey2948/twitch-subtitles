using System.Text.Json.Serialization;

namespace TwitchSubtitles.Web.Models;

/// <summary>
/// Тип сообщения WebSocket.
/// </summary>
public enum MessageTypeId
{
    /// <summary>
    /// Чанк аудио от клиента.
    /// </summary>
    AudioChunk,

    /// <summary>
    /// Субтитры от сервера.
    /// </summary>
    Subtitle,

    /// <summary>
    /// Ошибка.
    /// </summary>
    Error,

    /// <summary>
    /// Обновление настроек.
    /// </summary>
    SettingsUpdate
}

/// <summary>
/// Базовое сообщение WebSocket.
/// </summary>
public abstract class WebSocketMessage
{
    /// <summary>
    /// Тип сообщения.
    /// </summary>
    [JsonPropertyName("type")]
    public MessageTypeId Type { get; set; }

    /// <summary>
    /// Временная метка.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }
}

/// <summary>
/// Сообщение с чанком аудио от клиента.
/// </summary>
public class AudioChunkMessage : WebSocketMessage
{
    /// <summary>
    /// Base64-encoded аудио данные (PCM/WAV).
    /// </summary>
    [JsonPropertyName("audio_data")]
    public string AudioData { get; set; } = string.Empty;
}

/// <summary>
/// Сообщение с субтитрами от сервера.
/// </summary>
public class SubtitleMessage : WebSocketMessage
{
    /// <summary>
    /// Распознанный русский текст.
    /// </summary>
    [JsonPropertyName("rus_text")]
    public string RusText { get; set; } = string.Empty;

    /// <summary>
    /// Перевод на английский.
    /// </summary>
    [JsonPropertyName("eng_text")]
    public string EngText { get; set; } = string.Empty;
}

/// <summary>
/// Сообщение об ошибке.
/// </summary>
public class ErrorMessage : WebSocketMessage
{
    /// <summary>
    /// Текст ошибки.
    /// </summary>
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Сообщение с обновлением настроек оверлея.
/// </summary>
public class SettingsUpdateMessage : WebSocketMessage
{
    [JsonPropertyName("overlay_font_size")]
    public int OverlayFontSize { get; set; }

    [JsonPropertyName("overlay_font_color")]
    public string OverlayFontColor { get; set; } = string.Empty;

    [JsonPropertyName("overlay_bg_opacity")]
    public double OverlayBgOpacity { get; set; }

    [JsonPropertyName("overlay_display_duration_ms")]
    public int OverlayDisplayDurationMs { get; set; }

    [JsonPropertyName("overlay_show_translation")]
    public bool OverlayShowTranslation { get; set; }

    [JsonPropertyName("overlay_show_original")]
    public bool OverlayShowOriginal { get; set; }
}
