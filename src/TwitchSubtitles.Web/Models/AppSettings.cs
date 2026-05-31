using System.Text.Json.Serialization;

namespace TwitchSubtitles.Web.Models;

public class AppSettings
{
    // STT settings
    [JsonPropertyName("model")]
    public string Model { get; set; } = "base";

    [JsonPropertyName("vad_enabled")]
    public bool VadEnabled { get; set; } = true;

    [JsonPropertyName("chunk_duration_ms")]
    public int ChunkDurationMs { get; set; } = 3000;

    [JsonPropertyName("overlap_ms")]
    public int OverlapMs { get; set; } = 0;

    [JsonPropertyName("dedup_enabled")]
    public bool DedupEnabled { get; set; } = true;

    // Overlay settings
    [JsonPropertyName("overlay_font_size")]
    public int OverlayFontSize { get; set; } = 24;

    [JsonPropertyName("overlay_font_color")]
    public string OverlayFontColor { get; set; } = "#FFFFFF";

    [JsonPropertyName("overlay_bg_opacity")]
    public double OverlayBgOpacity { get; set; } = 0.7;

    [JsonPropertyName("overlay_display_duration_ms")]
    public int OverlayDisplayDurationMs { get; set; } = 5000;

    [JsonPropertyName("overlay_show_translation")]
    public bool OverlayShowTranslation { get; set; } = true;
}

public class AppSettingsUpdate
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("vad_enabled")]
    public bool? VadEnabled { get; set; }

    [JsonPropertyName("chunk_duration_ms")]
    public int? ChunkDurationMs { get; set; }

    [JsonPropertyName("overlap_ms")]
    public int? OverlapMs { get; set; }

    [JsonPropertyName("dedup_enabled")]
    public bool? DedupEnabled { get; set; }

    [JsonPropertyName("overlay_font_size")]
    public int? OverlayFontSize { get; set; }

    [JsonPropertyName("overlay_font_color")]
    public string? OverlayFontColor { get; set; }

    [JsonPropertyName("overlay_bg_opacity")]
    public double? OverlayBgOpacity { get; set; }

    [JsonPropertyName("overlay_display_duration_ms")]
    public int? OverlayDisplayDurationMs { get; set; }

    [JsonPropertyName("overlay_show_translation")]
    public bool? OverlayShowTranslation { get; set; }
}
