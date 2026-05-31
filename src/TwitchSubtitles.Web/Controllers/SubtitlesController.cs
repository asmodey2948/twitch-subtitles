using Microsoft.AspNetCore.Mvc;
using TwitchSubtitles.Web.Models;
using TwitchSubtitles.Web.Services;

namespace TwitchSubtitles.Web.Controllers;

/// <summary>
/// Контроллер для обработки аудиофайлов и возврата субтитров.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SubtitlesController : ControllerBase
{
    /// <summary>
    /// Клиент ML сервиса для транскрибации и перевода.
    /// </summary>
    private readonly IMlServiceClient _mlClient;

    /// <summary>
    /// Логгер.
    /// </summary>
    private readonly ILogger<SubtitlesController> _logger;

    public SubtitlesController(IMlServiceClient mlClient, ILogger<SubtitlesController> logger)
    {
        _mlClient = mlClient;
        _logger = logger;
    }

    /// <summary>
    /// Принимает аудиофайл, отправляет его в ML сервис и возвращает распознанный текст с переводом.
    /// </summary>
    /// <param name="file">Аудиофайл (.mp3 или .wav).</param>
    /// <returns>Распознанный русский текст и его перевод на английский.</returns>
    [HttpPost]
    public async Task<IActionResult> Upload(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("File is empty or not provided.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".mp3" or ".wav"))
        {
            return BadRequest($"Unsupported format: {extension}. Use .mp3 or .wav");
        }

        var contentType = extension == ".mp3" ? "audio/mpeg" : "audio/wav";

        try
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();

            var result = await _mlClient.ProcessAudioAsync(file.FileName, fileBytes, contentType);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid file uploaded: {FileName}", file.FileName);
            return BadRequest(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "ML service request failed");
            return StatusCode(500, "ML service is unavailable.");
        }
    }
}
