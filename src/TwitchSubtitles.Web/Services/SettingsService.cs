using System.Text.Json;
using TwitchSubtitles.Web.Models;

namespace TwitchSubtitles.Web.Services;

public class SettingsService
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private AppSettings _settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public SettingsService(IConfiguration configuration)
        : this(Path.Combine(
            Path.GetDirectoryName(configuration["__SETTINGS_DIR__"]) ?? AppContext.BaseDirectory,
            "settings.json"))
    {
    }

    public SettingsService(string filePath)
    {
        _filePath = filePath;
        _settings = LoadFromDisk();
    }

    public AppSettings GetSettings()
    {
        lock (_lock)
        {
            return new AppSettings
            {
                Model = _settings.Model,
                VadEnabled = _settings.VadEnabled,
                ChunkDurationMs = _settings.ChunkDurationMs,
                OverlapMs = _settings.OverlapMs,
                DedupEnabled = _settings.DedupEnabled,
                OverlayFontSize = _settings.OverlayFontSize,
                OverlayFontColor = _settings.OverlayFontColor,
                OverlayBgOpacity = _settings.OverlayBgOpacity,
                OverlayDisplayDurationMs = _settings.OverlayDisplayDurationMs,
                OverlayShowTranslation = _settings.OverlayShowTranslation
            };
        }
    }

    public AppSettings UpdateSettings(AppSettingsUpdate patch)
    {
        lock (_lock)
        {
            if (patch.Model is not null) _settings.Model = patch.Model;
            if (patch.VadEnabled.HasValue) _settings.VadEnabled = patch.VadEnabled.Value;
            if (patch.ChunkDurationMs.HasValue) _settings.ChunkDurationMs = patch.ChunkDurationMs.Value;
            if (patch.OverlapMs.HasValue) _settings.OverlapMs = patch.OverlapMs.Value;
            if (patch.DedupEnabled.HasValue) _settings.DedupEnabled = patch.DedupEnabled.Value;
            if (patch.OverlayFontSize.HasValue) _settings.OverlayFontSize = patch.OverlayFontSize.Value;
            if (patch.OverlayFontColor is not null) _settings.OverlayFontColor = patch.OverlayFontColor;
            if (patch.OverlayBgOpacity.HasValue) _settings.OverlayBgOpacity = patch.OverlayBgOpacity.Value;
            if (patch.OverlayDisplayDurationMs.HasValue) _settings.OverlayDisplayDurationMs = patch.OverlayDisplayDurationMs.Value;
            if (patch.OverlayShowTranslation.HasValue) _settings.OverlayShowTranslation = patch.OverlayShowTranslation.Value;

            SaveToDisk();
            return GetSettings();
        }
    }

    private AppSettings LoadFromDisk()
    {
        if (!File.Exists(_filePath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    private void SaveToDisk()
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }
}
