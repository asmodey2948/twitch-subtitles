using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TwitchSubtitles.Web.Tests;

/// <summary>
/// Интеграционные тесты для Frontend: static files и API доступность.
/// </summary>
public class FrontendTests : IClassFixture<WebApplicationFactory<Program>>
{
    /// <summary>
    /// Фабрика тестового приложения.
    /// </summary>
    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>
    /// HTTP-клиент для тестовых запросов.
    /// </summary>
    private readonly HttpClient _client;

    public FrontendTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task IndexHtml_Returns200()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task IndexHtml_ContainsTitle()
    {
        var response = await _client.GetAsync("/");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("Twitch Subtitles", content);
    }

    [Fact]
    public async Task SiteCss_Returns200()
    {
        var response = await _client.GetAsync("/css/site.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AppJs_Returns200()
    {
        var response = await _client.GetAsync("/js/app.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ApiSubtitles_WithoutFile_ReturnsError()
    {
        var response = await _client.PostAsync("/api/subtitles", null);
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task OverlayHtml_Returns200()
    {
        var response = await _client.GetAsync("/overlay.html");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OverlayHtml_ContainsSubtitleContainer()
    {
        var response = await _client.GetAsync("/overlay.html");
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("subtitleOverlay", content);
    }

    [Fact]
    public async Task OverlayCss_Returns200()
    {
        var response = await _client.GetAsync("/css/overlay.css");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OverlayJs_Returns200()
    {
        var response = await _client.GetAsync("/js/overlay.js");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
