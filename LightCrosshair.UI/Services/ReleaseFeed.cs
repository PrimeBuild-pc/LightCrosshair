using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace LightCrosshair.UI.Services;

public interface IReleaseFeed
{
    Task<string?> GetLatestTagAsync(CancellationToken cancellationToken);
}

public sealed class GitHubReleaseFeed : IReleaseFeed
{
    public const string ApiUrl = "https://api.github.com/repos/PrimeBuild-pc/LightCrosshair/releases/latest";
    public const string ReleasesUrl = "https://github.com/PrimeBuild-pc/LightCrosshair/releases/latest";

    private static readonly HttpClient Http = Create();

    public async Task<string?> GetLatestTagAsync(CancellationToken cancellationToken)
    {
        try
        {
            var dto = await Http.GetFromJsonAsync<ReleaseDto>(ApiUrl, cancellationToken).ConfigureAwait(false);
            var tag = dto?.TagName ?? "";
            return string.IsNullOrWhiteSpace(tag) ? null : UpdateVersionComparer.NormalizeVersionTag(tag);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static HttpClient Create()
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        var version = typeof(GitHubReleaseFeed).Assembly.GetName().Version;
        http.DefaultRequestHeaders.UserAgent.ParseAdd($"LightCrosshair/{version?.Major}.{version?.Minor}.{version?.Build}");
        return http;
    }

    private sealed class ReleaseDto
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }
    }
}
