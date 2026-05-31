using System.Text.Json.Serialization;

namespace TwitchSubtitles.Web.Models;

public class MlModelsResponse
{
    [JsonPropertyName("models")]
    public List<string> Models { get; set; } = new();

    [JsonPropertyName("current")]
    public string Current { get; set; } = string.Empty;

    [JsonPropertyName("vad_enabled")]
    public bool VadEnabled { get; set; } = true;
}

public class MlSettingsRequest
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("vad_enabled")]
    public bool? VadEnabled { get; set; }
}

public class MlSettingsResponse
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("vad_enabled")]
    public bool VadEnabled { get; set; } = true;
}
