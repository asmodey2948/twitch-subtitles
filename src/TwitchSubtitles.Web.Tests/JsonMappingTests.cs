using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TwitchSubtitles.Web.Models;
using TwitchSubtitles.Web.Services;
using Xunit;

namespace TwitchSubtitles.Web.Tests;

/// <summary>
/// Регрессионные тесты для бага: рассинхрон JSON-полей между Python ML Service и C# Backend.
/// Python возвращает snake_case (rus_text, eng_text), C# должен корректно десериализовать.
/// </summary>
public class JsonMappingTests
{
    /// <summary>
    /// JSON от Python ML Service в snake_case — должен мапиться на PascalCase свойства C#.
    /// </summary>
    [Fact]
    public void SubtitlesResponse_Deserializes_SnakeCase()
    {
        var json = """{"rus_text": "Привет", "eng_text": "Hello"}""";
        var result = JsonSerializer.Deserialize<SubtitlesResponse>(json);

        Assert.NotNull(result);
        Assert.Equal("Привет", result.RusText);
        Assert.Equal("Hello", result.EngText);
    }

    /// <summary>
    /// C# модель сериализуется в snake_case — чтобы совпадать с контрактом Python.
    /// </summary>
    [Fact]
    public void SubtitlesResponse_Serializes_SnakeCase()
    {
        var response = new SubtitlesResponse { RusText = "Тест", EngText = "Test" };
        var json = JsonSerializer.Serialize(response);

        Assert.Contains("\"rus_text\"", json);
        Assert.Contains("\"eng_text\"", json);
        Assert.DoesNotContain("\"rusText\"", json);
        Assert.DoesNotContain("\"engText\"", json);
    }

    /// <summary>
    /// Пустые строки от ML сервиса корректно десериализуются.
    /// </summary>
    [Fact]
    public void SubtitlesResponse_Deserializes_EmptyStrings()
    {
        var json = """{"rus_text": "", "eng_text": ""}""";
        var result = JsonSerializer.Deserialize<SubtitlesResponse>(json);

        Assert.NotNull(result);
        Assert.Equal("", result.RusText);
        Assert.Equal("", result.EngText);
    }

    /// <summary>
    /// MlServiceClient корректно парсит реальный ответ от Python ML Service.
    /// </summary>
    [Fact]
    public async Task MlServiceClient_ParsesRealPythonResponse()
    {
        // Точный формат, который возвращает FastAPI
        var pythonJson = """{"rus_text":"Привет, это тест.","eng_text":"Hello, this is a test."}""";
        var handler = new MockHttpMessageHandler(pythonJson, HttpStatusCode.OK);
        var httpClient = new HttpClient(handler);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { { "MlService:BaseUrl", "http://localhost:8000" } })
            .Build();
        var client = new MlServiceClient(httpClient, config);

        var result = await client.ProcessAudioAsync("test.wav", [1, 2, 3], "audio/wav");

        Assert.Equal("Привет, это тест.", result.RusText);
        Assert.Equal("Hello, this is a test.", result.EngText);
    }
}
