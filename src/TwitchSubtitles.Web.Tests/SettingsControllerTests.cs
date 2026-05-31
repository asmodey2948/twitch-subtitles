using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TwitchSubtitles.Web.Controllers;
using TwitchSubtitles.Web.Models;
using TwitchSubtitles.Web.Services;
using Xunit;

namespace TwitchSubtitles.Web.Tests;

public class SettingsControllerTests : IDisposable
{
    private readonly Mock<IMlServiceClient> _mlClientMock;
    private readonly Mock<ILogger<SettingsController>> _loggerMock;
    private readonly SettingsService _settingsService;
    private readonly SettingsController _controller;
    private readonly string _tempFile;

    public SettingsControllerTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"settings_ctrl_test_{Guid.NewGuid()}.json");
        _mlClientMock = new Mock<IMlServiceClient>();
        _loggerMock = new Mock<ILogger<SettingsController>>();
        _settingsService = new SettingsService(_tempFile);
        _controller = new SettingsController(_settingsService, _mlClientMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile)) File.Delete(_tempFile);
    }

    [Fact]
    public async Task GetSettings_ReturnsDefaults_WhenMlAvailable()
    {
        _mlClientMock.Setup(m => m.GetModelsAsync()).ReturnsAsync(new MlModelsResponse
        {
            Models = ["tiny", "base", "small", "medium", "large"],
            Current = "base",
            VadEnabled = true
        });

        var result = await _controller.GetSettings();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var settings = Assert.IsType<AppSettings>(okResult.Value);
        Assert.Equal("base", settings.Model);
        Assert.True(settings.VadEnabled);
        Assert.Equal(3000, settings.ChunkDurationMs);
        Assert.Equal(0, settings.OverlapMs);
    }

    [Fact]
    public async Task GetSettings_ReturnsLocalDefaults_WhenMlUnavailable()
    {
        _mlClientMock.Setup(m => m.GetModelsAsync()).ThrowsAsync(new HttpRequestException());

        var result = await _controller.GetSettings();

        var okResult = Assert.IsType<OkObjectResult>(result);
        var settings = Assert.IsType<AppSettings>(okResult.Value);
        Assert.Equal(3000, settings.ChunkDurationMs);
    }

    [Fact]
    public async Task UpdateSettings_ChangesChunkDuration()
    {
        _mlClientMock.Setup(m => m.GetModelsAsync()).ReturnsAsync(new MlModelsResponse
        {
            Current = "base", VadEnabled = true
        });

        var patch = new AppSettingsUpdate { ChunkDurationMs = 5000 };
        var result = await _controller.UpdateSettings(patch);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var settings = Assert.IsType<AppSettings>(okResult.Value);
        Assert.Equal(5000, settings.ChunkDurationMs);
    }

    [Fact]
    public async Task UpdateSettings_ProxiesModelChangeToMl()
    {
        _mlClientMock.Setup(m => m.GetModelsAsync()).ReturnsAsync(new MlModelsResponse
        {
            Current = "small", VadEnabled = true
        });
        _mlClientMock.Setup(m => m.UpdateSettingsAsync("small", null))
            .ReturnsAsync(new MlSettingsResponse { Model = "small", VadEnabled = true });

        var patch = new AppSettingsUpdate { Model = "small" };
        await _controller.UpdateSettings(patch);

        _mlClientMock.Verify(m => m.UpdateSettingsAsync("small", null), Times.Once);
    }

    [Fact]
    public async Task UpdateSettings_ProxiesVadChangeToMl()
    {
        _mlClientMock.Setup(m => m.GetModelsAsync()).ReturnsAsync(new MlModelsResponse
        {
            Current = "base", VadEnabled = false
        });
        _mlClientMock.Setup(m => m.UpdateSettingsAsync(null, false))
            .ReturnsAsync(new MlSettingsResponse { Model = "base", VadEnabled = false });

        var patch = new AppSettingsUpdate { VadEnabled = false };
        await _controller.UpdateSettings(patch);

        _mlClientMock.Verify(m => m.UpdateSettingsAsync(null, false), Times.Once);
    }

    [Fact]
    public async Task UpdateSettings_MlUnavailable_Returns500()
    {
        _mlClientMock.Setup(m => m.UpdateSettingsAsync(It.IsAny<string?>(), It.IsAny<bool?>()))
            .ThrowsAsync(new HttpRequestException());

        var patch = new AppSettingsUpdate { Model = "small" };
        var result = await _controller.UpdateSettings(patch);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}
