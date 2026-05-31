using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using TwitchSubtitles.Web.Models;
using TwitchSubtitles.Web.Services;
using Xunit;

namespace TwitchSubtitles.Web.Tests;

/// <summary>
/// Тесты для MlServiceClient.
/// </summary>
public class MlServiceClientTests
{
    /// <summary>
    /// Вспомогательный метод: создаёт IConfiguration с заданным URL.
    /// </summary>
    private static IConfiguration CreateConfig(string baseUrl = "http://localhost:8000")
    {
        var dict = new Dictionary<string, string?> { { "MlService:BaseUrl", baseUrl } };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task ProcessAudioAsync_ValidWav_ReturnsSubtitlesResponse()
    {
        // Arrange
        var expected = new SubtitlesResponse { RusText = "Привет", EngText = "Hello" };
        var json = JsonSerializer.Serialize(expected);
        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var client = new MlServiceClient(httpClient, CreateConfig("http://localhost:8000"));

        // Act
        var result = await client.ProcessAudioAsync("test.wav", [1, 2, 3], "audio/wav");

        // Assert
        Assert.Equal("Привет", result.RusText);
        Assert.Equal("Hello", result.EngText);
    }

    [Fact]
    public async Task ProcessAudioAsync_ValidMp3_ReturnsSubtitlesResponse()
    {
        // Arrange
        var expected = new SubtitlesResponse { RusText = "Тест", EngText = "Test" };
        var json = JsonSerializer.Serialize(expected);
        var handler = new MockHttpMessageHandler(json, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var client = new MlServiceClient(httpClient, CreateConfig("http://localhost:8000"));

        // Act
        var result = await client.ProcessAudioAsync("audio.mp3", [1, 2, 3], "audio/mpeg");

        // Assert
        Assert.Equal("Тест", result.RusText);
    }

    [Fact]
    public async Task ProcessAudioAsync_InvalidExtension_ThrowsArgumentException()
    {
        var handler = new MockHttpMessageHandler("", HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var client = new MlServiceClient(httpClient, CreateConfig("http://localhost:8000"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.ProcessAudioAsync("file.txt", [1, 2, 3], "text/plain"));
    }

    [Fact]
    public async Task ProcessAudioAsync_MlServiceReturns500_ThrowsHttpRequestException()
    {
        var handler = new MockHttpMessageHandler("error", HttpStatusCode.InternalServerError);
        var httpClient = new HttpClient(handler);
        var client = new MlServiceClient(httpClient, CreateConfig("http://localhost:8000"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.ProcessAudioAsync("test.wav", [1, 2, 3], "audio/wav"));
    }

    [Fact]
    public async Task ProcessAudioAsync_MlServiceReturns400_ThrowsArgumentException()
    {
        var handler = new MockHttpMessageHandler("bad request", HttpStatusCode.BadRequest);
        var httpClient = new HttpClient(handler);
        var client = new MlServiceClient(httpClient, CreateConfig("http://localhost:8000"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.ProcessAudioAsync("test.wav", [1, 2, 3], "audio/wav"));
    }
}

/// <summary>
/// Mock HttpMessageHandler для перехвата HTTP-запросов в тестах.
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    /// <summary>
    /// JSON-ответ, возвращаемый обработчиком.
    /// </summary>
    private readonly string _response;

    /// <summary>
    /// HTTP-статус ответа.
    /// </summary>
    private readonly HttpStatusCode _statusCode;

    public MockHttpMessageHandler(string response, HttpStatusCode statusCode)
    {
        _response = response;
        _statusCode = statusCode;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_response, Encoding.UTF8, "application/json")
        });
    }
}
