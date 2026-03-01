using Microsoft.Extensions.Logging;

namespace HopTracer.Web.Services;

public interface ITempFileManager
{
    ITempFileScope CreateScope();
    int CleanupStaleUploadFiles(TimeSpan maxAge);
}

public interface ITempFileScope : IDisposable
{
    void Track(string? path);
    void Cleanup();
}

public sealed class TempFileManager : ITempFileManager
{
    private static readonly string[] ManagedPrefixList = ["compare_", "review_", "git_"];
    private static readonly TimeSpan DefaultMaxAge = TimeSpan.FromHours(24);

    private readonly ILogger<TempFileManager> _logger;
    private readonly string _uploadsPath;

    public TempFileManager(IAppDataStorageService storage, ILogger<TempFileManager> logger)
    {
        _logger = logger;
        _uploadsPath = storage.UploadsPath;

        try
        {
            var deleted = CleanupStaleUploadFiles(DefaultMaxAge);
            if (deleted > 0)
            {
                _logger.LogInformation("Startup temp cleanup removed {DeletedCount} stale files.", deleted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Startup temp cleanup failed.");
        }
    }

    public ITempFileScope CreateScope()
    {
        return new TempFileScope(_logger);
    }

    public int CleanupStaleUploadFiles(TimeSpan maxAge)
    {
        if (maxAge <= TimeSpan.Zero || !Directory.Exists(_uploadsPath))
        {
            return 0;
        }

        var deleted = 0;
        var cutoff = DateTime.UtcNow - maxAge;
        foreach (var filePath in Directory.EnumerateFiles(_uploadsPath, "*", SearchOption.TopDirectoryOnly))
        {
            if (!IsManagedTempFile(filePath))
            {
                continue;
            }

            DateTime lastWriteUtc;
            try
            {
                lastWriteUtc = File.GetLastWriteTimeUtc(filePath);
            }
            catch
            {
                continue;
            }

            if (lastWriteUtc > cutoff)
            {
                continue;
            }

            try
            {
                File.Delete(filePath);
                deleted++;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to delete stale temp file {TempFile}", filePath);
            }
        }

        return deleted;
    }

    private static bool IsManagedTempFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return ManagedPrefixList.Any(prefix =>
            fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class TempFileScope : ITempFileScope
    {
        private readonly ILogger _logger;
        private readonly HashSet<string> _trackedPaths = new(StringComparer.OrdinalIgnoreCase);
        private bool _cleaned;

        public TempFileScope(ILogger logger)
        {
            _logger = logger;
        }

        public void Track(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch
            {
                return;
            }

            _trackedPaths.Add(fullPath);
        }

        public void Cleanup()
        {
            if (_cleaned)
            {
                return;
            }
            _cleaned = true;

            foreach (var path in _trackedPaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to delete tracked temp file {TempFile}", path);
                }
            }
        }

        public void Dispose()
        {
            Cleanup();
        }
    }
}
