using System.Net.Http.Headers;
using System.Text.Json;

namespace TiezhuToolbox;

internal sealed record GitHubReleaseInfo(
    Version Version,
    string TagName,
    string ReleasePageUrl,
    string? DownloadUrl);

/// <summary>从 GitHub Releases 查询最新稳定版，并选择 Windows x64 发布包。</summary>
internal static class GitHubUpdateService
{
    public const string RepositoryUrl = "https://github.com/Mooooooon/TiezhuToolboxPRO";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/Mooooooon/TiezhuToolboxPRO/releases/latest";

    public static Version CurrentVersion => NormalizeVersion(
        typeof(GitHubUpdateService).Assembly.GetName().Version ?? new Version(0, 0, 0, 0));

    public static string CurrentVersionText => $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

    public static async Task<GitHubReleaseInfo> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApiUrl);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("TiezhuToolboxPRO", CurrentVersion.ToString(3)));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;

        var tagName = root.GetProperty("tag_name").GetString()
            ?? throw new InvalidDataException("GitHub 最新发布缺少版本标签");
        var releasePageUrl = root.GetProperty("html_url").GetString()
            ?? throw new InvalidDataException("GitHub 最新发布缺少页面地址");
        var version = ParseVersionTag(tagName);

        string? downloadUrl = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString();
                if (name == null
                    || !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
                    || !name.Contains("win-x64", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                downloadUrl = asset.GetProperty("browser_download_url").GetString();
                break;
            }
        }

        return new GitHubReleaseInfo(version, tagName, releasePageUrl, downloadUrl);
    }

    internal static Version ParseVersionTag(string tagName)
    {
        var value = tagName.Trim().TrimStart('v', 'V');
        var suffixIndex = value.IndexOfAny(['-', '+']);
        if (suffixIndex >= 0)
            value = value[..suffixIndex];
        if (!Version.TryParse(value, out var version))
            throw new InvalidDataException($"无法解析 GitHub 版本标签：{tagName}");
        return NormalizeVersion(version);
    }

    private static Version NormalizeVersion(Version version)
        => new(
            version.Major,
            Math.Max(0, version.Minor),
            Math.Max(0, version.Build),
            Math.Max(0, version.Revision));
}
