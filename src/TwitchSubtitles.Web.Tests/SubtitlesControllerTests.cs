using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using TwitchSubtitles.Web.Controllers;
using TwitchSubtitles.Web.Models;
using TwitchSubtitles.Web.Services;
using Xunit;

namespace TwitchSubtitles.Web.Tests;

/// <summary>
/// Тесты для SubtitlesController.
/// </summary>
public class SubtitlesControllerTests
{
    /// <summary>
    /// Mock ML-сервиса.
    /// </summary>
    private readonly Mock<IMlServiceClient> _mlClientMock;

    /// <summary>
    /// Mock логгера.
    /// </summary>
    private readonly Mock<ILogger<SubtitlesController>> _loggerMock;

    /// <summary>
    /// Тестируемый контроллер.
    /// </summary>
    private readonly SubtitlesController _controller;

    public SubtitlesControllerTests()
    {
        _mlClientMock = new Mock<IMlServiceClient>();
        _loggerMock = new Mock<ILogger<SubtitlesController>>();
        _controller = new SubtitlesController(_mlClientMock.Object, _loggerMock.Object);
    }

    /// <summary>
    /// Вспомогательный метод: создаёт IFormFile с заданным именем и содержимым.
    /// </summary>
    private static IFormFile CreateFormFile(string fileName, byte[] content)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName);
    }

    [Fact]
    public async Task Upload_NullFile_Returns400()
    {
        var result = await _controller.Upload(null!);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("empty", badRequest.Value!.ToString()!.ToLower());
    }

    [Fact]
    public async Task Upload_EmptyFile_Returns400()
    {
        var file = CreateFormFile("test.wav", []);
        var result = await _controller.Upload(file);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Upload_InvalidExtension_Returns400()
    {
        var file = CreateFormFile("test.txt", [1, 2, 3]);
        var result = await _controller.Upload(file);
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("unsupported", badRequest.Value!.ToString()!.ToLower());
    }

    [Fact]
    public async Task Upload_ValidWaf_CallsMlServiceAndReturns200()
    {
        // Arrange
        var expected = new SubtitlesResponse { RusText = "Привет", EngText = "Hello" };
        _mlClientMock
            .Setup(m => m.ProcessAudioAsync("test.wav", It.IsAny<byte[]>(), "audio/wav"))
            .ReturnsAsync(expected);
        var file = CreateFormFile("test.wav", [1, 2, 3, 4]);

        // Act
        var result = await _controller.Upload(file);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SubtitlesResponse>(okResult.Value);
        Assert.Equal("Привет", response.RusText);
        Assert.Equal("Hello", response.EngText);
    }

    [Fact]
    public async Task Upload_ValidMp3_CallsMlServiceAndReturns200()
    {
        var expected = new SubtitlesResponse { RusText = "Тест", EngText = "Test" };
        _mlClientMock
            .Setup(m => m.ProcessAudioAsync("audio.mp3", It.IsAny<byte[]>(), "audio/mpeg"))
            .ReturnsAsync(expected);
        var file = CreateFormFile("audio.mp3", [1, 2, 3, 4]);

        var result = await _controller.Upload(file);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SubtitlesResponse>(okResult.Value);
        Assert.Equal("Тест", response.RusText);
    }

    [Fact]
    public async Task Upload_MlServiceThrowsHttpRequestException_Returns500()
    {
        _mlClientMock
            .Setup(m => m.ProcessAudioAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<string>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));
        var file = CreateFormFile("test.wav", [1, 2, 3, 4]);

        var result = await _controller.Upload(file);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }
}
