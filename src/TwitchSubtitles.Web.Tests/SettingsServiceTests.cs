using TwitchSubtitles.Web.Models;
using TwitchSubtitles.Web.Services;
using Xunit;

namespace TwitchSubtitles.Web.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempFile;
    private readonly SettingsService _service;

    public SettingsServiceTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid()}.json");
        _service = new SettingsService(_tempFile);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public void GetSettings_ReturnsDefaults()
    {
        var settings = _service.GetSettings();
        Assert.Equal("base", settings.Model);
        Assert.True(settings.VadEnabled);
        Assert.Equal(3000, settings.ChunkDurationMs);
        Assert.Equal(0, settings.OverlapMs);
        Assert.True(settings.DedupEnabled);
        Assert.Equal(24, settings.OverlayFontSize);
        Assert.Equal("#FFFFFF", settings.OverlayFontColor);
        Assert.Equal(0.7, settings.OverlayBgOpacity);
        Assert.Equal(5000, settings.OverlayDisplayDurationMs);
        Assert.True(settings.OverlayShowTranslation);
    }

    [Fact]
    public void UpdateSettings_ChangesChunkDuration()
    {
        var result = _service.UpdateSettings(new AppSettingsUpdate { ChunkDurationMs = 5000 });
        Assert.Equal(5000, result.ChunkDurationMs);

        var current = _service.GetSettings();
        Assert.Equal(5000, current.ChunkDurationMs);
    }

    [Fact]
    public void UpdateSettings_PartialUpdate_PreservesOtherFields()
    {
        _service.UpdateSettings(new AppSettingsUpdate { ChunkDurationMs = 6000 });
        var result = _service.UpdateSettings(new AppSettingsUpdate { OverlapMs = 1000 });

        Assert.Equal(6000, result.ChunkDurationMs);
        Assert.Equal(1000, result.OverlapMs);
    }

    [Fact]
    public void UpdateSettings_OverlayFontSize()
    {
        var result = _service.UpdateSettings(new AppSettingsUpdate { OverlayFontSize = 36 });
        Assert.Equal(36, result.OverlayFontSize);

        var current = _service.GetSettings();
        Assert.Equal(36, current.OverlayFontSize);
    }

    [Fact]
    public void UpdateSettings_OverlayBgOpacity()
    {
        var result = _service.UpdateSettings(new AppSettingsUpdate { OverlayBgOpacity = 0.5 });
        Assert.Equal(0.5, result.OverlayBgOpacity);
    }
}
