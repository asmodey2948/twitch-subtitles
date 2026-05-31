using System.Text.Json.Serialization;

namespace TwitchSubtitles.Web.Models;

/// <summary>
/// Результат обработки аудиофайла: распознанный русский текст и его перевод на английский.
/// </summary>
public class SubtitlesResponse
{
    /// <summary>
    /// Текст, распознанный из аудио на русском языке.
    /// </summary>
    [JsonPropertyName("rus_text")]
    public string RusText { get; set; } = string.Empty;

    /// <summary>
    /// Перевод распознанного текста на английский язык.
    /// </summary>
    [JsonPropertyName("eng_text")]
    public string EngText { get; set; } = string.Empty;
}
