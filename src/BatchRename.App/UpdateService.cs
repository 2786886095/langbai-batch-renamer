using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BatchRename.App;

public sealed record UpdateRelease(
    Version Version,
    string Tag,
    string Name,
    string Notes,
    Uri PageUri,
    Uri DownloadUri,
    string AssetName,
    long AssetSize,
    string? Sha256);

public sealed class UpdateService
{
    public const string RepositoryUrl = "https://github.com/2786886095/langbai-batch-renamer";
    public const string ReleasesUrl = RepositoryUrl + "/releases";
    private const string ApiLatestUrl = "https://api.github.com/repos/2786886095/langbai-batch-renamer/releases/latest";
    private readonly HttpClient _httpClient;

    public UpdateService(HttpMessageHandler? handler = null)
    {
        _httpClient = handler is null ? new HttpClient() : new HttpClient(handler);
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LangBai-BatchRename-Updater/1.1");
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    public async Task<UpdateRelease?> CheckAsync(CancellationToken cancellationToken = default)
    {
        UpdateRelease latest;
        try
        {
            latest = await CheckApiAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            latest = await CheckRedirectAsync(cancellationToken);
        }
        return latest.Version > CurrentVersion ? latest : null;
    }

    private async Task<UpdateRelease> CheckApiAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(ApiLatestUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? throw new InvalidDataException("GitHub Release 缺少版本标签。");
        var version = ParseVersion(tag);
        var assetName = $"BatchRename-Setup-{version.Major}.{version.Minor}.{version.Build}-x64.exe";
        var asset = root.GetProperty("assets").EnumerateArray()
            .FirstOrDefault(item => string.Equals(item.GetProperty("name").GetString(), assetName, StringComparison.OrdinalIgnoreCase));
        if (asset.ValueKind == JsonValueKind.Undefined) throw new InvalidDataException($"最新版缺少安装包：{assetName}");
        var digest = asset.TryGetProperty("digest", out var digestElement) ? digestElement.GetString() : null;
        return new UpdateRelease(
            version,
            tag,
            root.GetProperty("name").GetString() ?? $"浪白重命名工具 {tag}",
            root.GetProperty("body").GetString() ?? string.Empty,
            new Uri(root.GetProperty("html_url").GetString()!),
            new Uri(asset.GetProperty("browser_download_url").GetString()!),
            assetName,
            asset.GetProperty("size").GetInt64(),
            NormalizeDigest(digest));
    }

    private async Task<UpdateRelease> CheckRedirectAsync(CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("LangBai-BatchRename-Updater/1.1");
        using var response = await client.GetAsync(ReleasesUrl + "/latest", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.Found or HttpStatusCode.MovedPermanently or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect))
            response.EnsureSuccessStatusCode();
        var location = response.Headers.Location ?? throw new InvalidDataException("无法确定 GitHub 最新版本。");
        var tag = location.Segments.Last().Trim('/');
        var version = ParseVersion(tag);
        var versionText = $"{version.Major}.{version.Minor}.{version.Build}";
        var assetName = $"BatchRename-Setup-{versionText}-x64.exe";
        var sha256 = await TryReadTaggedSha256Async(client, tag, cancellationToken);
        return new UpdateRelease(
            version,
            tag,
            $"浪白重命名工具 {tag}",
            "请打开版本页面查看本次更新说明。",
            new Uri($"{RepositoryUrl}/releases/tag/{tag}"),
            new Uri($"{RepositoryUrl}/releases/download/{tag}/{assetName}"),
            assetName,
            0,
            sha256);
    }

    private static async Task<string?> TryReadTaggedSha256Async(HttpClient client, string tag, CancellationToken cancellationToken)
    {
        try
        {
            var notes = await client.GetStringAsync($"https://raw.githubusercontent.com/2786886095/langbai-batch-renamer/{tag}/RELEASE_NOTES.md", cancellationToken);
            return Regex.Match(notes, @"\b[A-Fa-f0-9]{64}\b", RegexOptions.CultureInvariant).Value.ToUpperInvariant() is { Length: 64 } hash ? hash : null;
        }
        catch (HttpRequestException) { return null; }
    }

    public async Task<string> DownloadAsync(UpdateRelease release, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(release.Sha256))
            throw new InvalidDataException("无法取得官方安装包的 SHA-256 校验值，已停止自动安装。请从 GitHub 版本页面手动下载。");
        var directory = Path.Combine(Path.GetTempPath(), "LangBai.BatchRename", "Updates", release.Tag);
        Directory.CreateDirectory(directory);
        var finalPath = Path.Combine(directory, release.AssetName);
        var partialPath = finalPath + ".download";
        using var response = await _httpClient.GetAsync(release.DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength ?? release.AssetSize;
        await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var target = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
        {
            var buffer = new byte[81920];
            long downloaded = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                downloaded += read;
                if (total > 0) progress.Report(downloaded * 100d / total);
            }
        }
        if (!string.IsNullOrWhiteSpace(release.Sha256))
        {
            await using var stream = File.OpenRead(partialPath);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
            if (!string.Equals(actual, release.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(partialPath);
                throw new InvalidDataException("安装包 SHA-256 校验失败，已停止安装并删除临时文件。");
            }
        }
        File.Move(partialPath, finalPath, true);
        progress.Report(100);
        return finalPath;
    }

    public static void StartInstaller(string path)
    {
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public static Version ParseVersion(string tag)
    {
        if (!Version.TryParse(tag.Trim().TrimStart('v', 'V'), out var version) || version.Build < 0)
            throw new InvalidDataException($"无法识别版本标签：{tag}");
        return version;
    }

    private static string? NormalizeDigest(string? digest)
    {
        if (string.IsNullOrWhiteSpace(digest)) return null;
        return digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase) ? digest[7..].ToUpperInvariant() : null;
    }
}
