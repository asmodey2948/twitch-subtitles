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
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(SettingsService settingsService, IMlServiceClient mlClient, ILogger<SettingsController> logger)
    {
        _settingsService = settingsService;
        _mlClient = mlClient;
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
        return Ok(updated);
    }
}
