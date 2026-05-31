using System.Net;
using TwitchSubtitles.Web.Models;

namespace TwitchSubtitles.Web.Services;

public class MlServiceClient : IMlServiceClient
{
    private readonly HttpClient _httpClient;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav" };

    public MlServiceClient(HttpClient httpClient, IConfiguration configuration)
    {
        var baseUrl = configuration["MlService:BaseUrl"] ?? "http://127.0.0.1:8000";
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(baseUrl);
    }

    public async Task<SubtitlesResponse> ProcessAudioAsync(string fileName, byte[] fileContent, string contentType)
    {
        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
            throw new ArgumentException($"Unsupported format: {extension}. Use .mp3 or .wav");

        using var content = new MultipartFormDataContent();
        var fileBytes = new ByteArrayContent(fileContent);
        fileBytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(fileBytes, "file", fileName);

        var response = await _httpClient.PostAsync("/process-audio", content);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException("ML service rejected the file format.");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SubtitlesResponse>()
            ?? throw new InvalidOperationException("ML service returned empty response.");
    }

    public async Task<SubtitlesResponse> ProcessAudioChunkAsync(byte[] audioData)
    {
        using var content = new MultipartFormDataContent();
        var fileBytes = new ByteArrayContent(audioData);
        fileBytes.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        content.Add(fileBytes, "audio", "chunk.wav");

        var response = await _httpClient.PostAsync("/process-chunk", content);
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new ArgumentException("ML service rejected the audio chunk.");
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SubtitlesResponse>()
            ?? throw new InvalidOperationException("ML service returned empty response.");
    }

    public async Task<MlModelsResponse> GetModelsAsync()
    {
        var response = await _httpClient.GetAsync("/models");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MlModelsResponse>()
            ?? throw new InvalidOperationException("ML service returned empty response.");
    }

    public async Task<MlSettingsResponse> UpdateSettingsAsync(string? model, bool? vadEnabled)
    {
        var request = new { model, vad_enabled = vadEnabled };
        var response = await _httpClient.PutAsJsonAsync("/settings", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MlSettingsResponse>()
            ?? throw new InvalidOperationException("ML service returned empty response.");
    }

    public async Task<MlVersionResponse> GetVersionAsync()
    {
        var response = await _httpClient.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MlVersionResponse>()
            ?? throw new InvalidOperationException("ML service returned empty response.");
    }

    public async Task<DownloadProgressResponse> GetDownloadProgressAsync()
    {
        var response = await _httpClient.GetAsync("/download-progress");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<DownloadProgressResponse>()
            ?? new DownloadProgressResponse { Status = "idle", Progress = 0 };
    }

    public async Task<ClearCacheResponse> ClearModelsCacheAsync()
    {
        var response = await _httpClient.DeleteAsync("/models/cache");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ClearCacheResponse>()
            ?? new ClearCacheResponse { Status = "ok", Message = "Cache cleared", FreedBytes = 0, FreedMb = 0 };
    }

    public async Task<CachedModelsResponse> GetCachedModelsAsync()
    {
        var response = await _httpClient.GetAsync("/models/cached");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CachedModelsResponse>()
            ?? new CachedModelsResponse { Models = new List<CachedModelInfo>() };
    }
}
