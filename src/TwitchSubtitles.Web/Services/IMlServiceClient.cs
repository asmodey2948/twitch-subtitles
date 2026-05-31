using TwitchSubtitles.Web.Models;

namespace TwitchSubtitles.Web.Services;

public interface IMlServiceClient
{
    Task<SubtitlesResponse> ProcessAudioAsync(string fileName, byte[] fileContent, string contentType);
    Task<SubtitlesResponse> ProcessAudioChunkAsync(byte[] audioData);
    Task<MlModelsResponse> GetModelsAsync();
    Task<MlSettingsResponse> UpdateSettingsAsync(string? model, bool? vadEnabled);
}
