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

public class MlVersionResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
}

public class DownloadProgressResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = "idle";  // idle, downloading, loading, complete, error

    [JsonPropertyName("progress")]
    public int Progress { get; set; } = 0;  // 0-100

    [JsonPropertyName("total")]
    public long Total { get; set; } = 100;

    [JsonPropertyName("current")]
    public long Current { get; set; } = 0;

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class ClearCacheResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("freed_bytes")]
    public long FreedBytes { get; set; }

    [JsonPropertyName("freed_mb")]
    public double FreedMb { get; set; }
}

public class CachedModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("cached")]
    public bool Cached { get; set; }

    [JsonPropertyName("size_mb")]
    public double SizeMb { get; set; }
}

public class CachedModelsResponse
{
    [JsonPropertyName("models")]
    public List<CachedModelInfo> Models { get; set; } = new();
}
