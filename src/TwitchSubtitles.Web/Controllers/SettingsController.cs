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

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] AppSettingsUpdate patch)
    {
        var current = _settingsService.GetSettings();

        var modelChanged = patch.Model is not null && patch.Model != current.Model;
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
            patch.OverlayTranslationBgOpacity.HasValue;

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
                OverlayTranslationBgOpacity = updated.OverlayTranslationBgOpacity
            };
            await _broadcaster.BroadcastSettingsAsync(settingsMessage);
        }

        return Ok(updated);
    }
}
