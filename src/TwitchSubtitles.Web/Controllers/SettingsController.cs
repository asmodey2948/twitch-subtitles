using Microsoft.AspNetCore.Mvc;
using TwitchSubtitles.Web.Models;
using TwitchSubtitles.Web.Services;

namespace TwitchSubtitles.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settingsService;
    private readonly IMlServiceClient _mlClient;
    private readonly WebSocketBroadcaster _broadcaster;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(SettingsService settingsService, IMlServiceClient mlClient, WebSocketBroadcaster broadcaster, ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _mlClient = mlClient;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var settings = _settingsService.GetSettings();

        try
        {
            var mlStatus = await _mlClient.GetModelsAsync();
            settings.Model = mlStatus.Current;
            settings.VadEnabled = mlStatus.VadEnabled;
        }
        catch (HttpRequestException)
        {
            _logger.LogWarning("ML service unavailable, returning local settings");
        }

        return Ok(settings);
    }

    [HttpGet("download-progress")]
    public async Task<IActionResult> GetDownloadProgress()
    {
        try
        {
            var progress = await _mlClient.GetDownloadProgressAsync();
            return Ok(progress);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ML service unavailable for download progress");
            return StatusCode(503, new DownloadProgressResponse
            {
                Status = "error",
                Error = "ML service is unavailable"
            });
        }
    }

    [HttpDelete("models-cache")]
    public async Task<IActionResult> ClearModelsCache()
    {
        try
        {
            var result = await _mlClient.ClearModelsCacheAsync();
            return Ok(result);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ML service unavailable for cache clearing");
            return StatusCode(503, new ClearCacheResponse
            {
                Status = "error",
                Message = "ML service is unavailable"
            });
        }
    }

    [HttpGet("models-cached")]
    public async Task<IActionResult> GetCachedModels()
    {
        try
        {
            var cached = await _mlClient.GetCachedModelsAsync();
            return Ok(cached);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ML service unavailable for cached models check");
            return StatusCode(503, new CachedModelsResponse
            {
                Models = new List<CachedModelInfo>()
            });
        }
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] AppSettingsUpdate patch)
    {
        var current = _settingsService.GetSettings();

        // Always forward model changes to ML service, even if same model
        // This allows for reload after cache clear
        var modelChanged = patch.Model is not null;
        var vadChanged = patch.VadEnabled.HasValue && patch.VadEnabled.Value != current.VadEnabled;

        if (modelChanged || vadChanged)
        {
            try
            {
                await _mlClient.UpdateSettingsAsync(
                    modelChanged ? patch.Model : null,
                    vadChanged ? patch.VadEnabled : null);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "ML service request failed");
                return StatusCode(500, "ML service is unavailable.");
            }
        }

        var updated = _settingsService.UpdateSettings(patch);

        // Broadcast overlay settings via WebSocket if they changed
        var overlaySettingsChanged =
            patch.OverlayFontSize.HasValue ||
            patch.OverlayFontColor is not null ||
            patch.OverlayBgOpacity.HasValue ||
            patch.OverlayDisplayDurationMs.HasValue ||
            patch.OverlayShowTranslation.HasValue ||
            patch.OverlayShowOriginal.HasValue ||
            patch.OverlayTranslationFontSize.HasValue ||
            patch.OverlayTranslationBgOpacity.HasValue ||
            patch.OverlayTranslationFontColor is not null;

        if (overlaySettingsChanged)
        {
            var settingsMessage = new SettingsUpdateMessage
            {
                OverlayFontSize = updated.OverlayFontSize,
                OverlayFontColor = updated.OverlayFontColor,
                OverlayBgOpacity = updated.OverlayBgOpacity,
                OverlayDisplayDurationMs = updated.OverlayDisplayDurationMs,
                OverlayShowTranslation = updated.OverlayShowTranslation,
                OverlayShowOriginal = updated.OverlayShowOriginal,
                OverlayTranslationFontSize = updated.OverlayTranslationFontSize,
                OverlayTranslationBgOpacity = updated.OverlayTranslationBgOpacity,
                OverlayTranslationFontColor = updated.OverlayTranslationFontColor
            };
            await _broadcaster.BroadcastSettingsAsync(settingsMessage);
        }

        return Ok(updated);
    }
}
