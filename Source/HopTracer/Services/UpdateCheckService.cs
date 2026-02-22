using System.Text.Json;
using Microsoft.Maui.ApplicationModel;

namespace HopTracer.Maui.Services;

public interface IUpdateCheckService
{
    Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default);
}

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    string? ErrorMessage);

public sealed class GitHubUpdateCheckService : IUpdateCheckService
{
    private const string LatestReleaseApi = "https://api.github.com/repos/lasaths/HopTracer/releases/latest";
    private static readonly HttpClient HttpClient = new();
    private static readonly object CacheLock = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);
    private static DateTimeOffset _lastCheckedUtc = DateTimeOffset.MinValue;
    private static UpdateCheckResult? _cached;

    static GitHubUpdateCheckService()
    {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("HopTracer/1.0");
        HttpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }

    public async Task<UpdateCheckResult> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        lock (CacheLock)
        {
            if (_cached is not null && DateTimeOffset.UtcNow - _lastCheckedUtc < CacheDuration)
            {
                return _cached;
            }
        }

        var currentVersion = NormalizeVersion(AppInfo.Current.VersionString);

        try
        {
            using var response = await HttpClient.GetAsync(LatestReleaseApi, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return CacheResult(new UpdateCheckResult(
                    IsUpdateAvailable: false,
                    CurrentVersion: currentVersion,
                    LatestVersion: null,
                    ReleaseUrl: null,
                    ErrorMessage: $"Release check failed ({(int)response.StatusCode})"));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = doc.RootElement;

            var tag = root.TryGetProperty("tag_name", out var tagElement) ? tagElement.GetString() : null;
            var latestVersion = NormalizeVersion(tag);
            var releaseUrl = root.TryGetProperty("html_url", out var urlElement) ? urlElement.GetString() : null;

            var isUpdateAvailable =
                TryParseVersion(currentVersion, out var current) &&
                TryParseVersion(latestVersion, out var latest) &&
                latest > current;

            return CacheResult(new UpdateCheckResult(
                IsUpdateAvailable: isUpdateAvailable,
                CurrentVersion: currentVersion,
                LatestVersion: latestVersion,
                ReleaseUrl: releaseUrl,
                ErrorMessage: null));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return CacheResult(new UpdateCheckResult(
                IsUpdateAvailable: false,
                CurrentVersion: currentVersion,
                LatestVersion: null,
                ReleaseUrl: null,
                ErrorMessage: ex.Message));
        }
    }

    private static UpdateCheckResult CacheResult(UpdateCheckResult result)
    {
        lock (CacheLock)
        {
            _cached = result;
            _lastCheckedUtc = DateTimeOffset.UtcNow;
        }
        return result;
    }

    private static string NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "0.0.0";
        }

        var normalized = version.Trim();
        if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..];
        }
        return normalized;
    }

    private static bool TryParseVersion(string? version, out Version parsed)
    {
        if (Version.TryParse(version, out parsed!))
        {
            return true;
        }

        parsed = new Version(0, 0, 0);
        return false;
    }
}
