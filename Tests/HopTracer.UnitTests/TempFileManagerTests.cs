using HopTracer.Web.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HopTracer.UnitTests;

public class TempFileManagerTests : IDisposable
{
    private readonly string _root;
    private readonly string _uploads;
    private readonly TempFileManager _manager;

    public TempFileManagerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"hoptracer_temp_tests_{Guid.NewGuid():N}");
        _uploads = Path.Combine(_root, "uploads");
        Directory.CreateDirectory(_uploads);
        Directory.CreateDirectory(Path.Combine(_root, "baselines"));
        _manager = new TempFileManager(new FakeStorage(_root), NullLogger<TempFileManager>.Instance);
    }

    [Fact]
    public void ScopeCleanup_DeletesTrackedFilesOnly()
    {
        var tracked = Path.Combine(_uploads, "compare_tracked.ghx");
        var untracked = Path.Combine(_uploads, "compare_untracked.ghx");
        File.WriteAllText(tracked, "tracked");
        File.WriteAllText(untracked, "untracked");

        using (var scope = _manager.CreateScope())
        {
            scope.Track(tracked);
        }

        Assert.False(File.Exists(tracked));
        Assert.True(File.Exists(untracked));
    }

    [Fact]
    public void CleanupStaleUploadFiles_RemovesOnlyOldManagedTempFiles()
    {
        var oldManaged = Path.Combine(_uploads, "compare_old.ghx");
        var oldUnmanaged = Path.Combine(_uploads, "custom_old.ghx");
        var recentManaged = Path.Combine(_uploads, "review_recent.ghx");

        File.WriteAllText(oldManaged, "old");
        File.WriteAllText(oldUnmanaged, "old");
        File.WriteAllText(recentManaged, "new");

        var staleTime = DateTime.UtcNow.AddDays(-2);
        File.SetLastWriteTimeUtc(oldManaged, staleTime);
        File.SetLastWriteTimeUtc(oldUnmanaged, staleTime);
        File.SetLastWriteTimeUtc(recentManaged, DateTime.UtcNow);

        var deleted = _manager.CleanupStaleUploadFiles(TimeSpan.FromHours(24));

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(oldManaged));
        Assert.True(File.Exists(oldUnmanaged));
        Assert.True(File.Exists(recentManaged));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup for temp test directories.
        }
    }

    private sealed class FakeStorage : IAppDataStorageService
    {
        public FakeStorage(string rootPath)
        {
            RootPath = rootPath;
            UploadsPath = Path.Combine(rootPath, "uploads");
            BaselinesPath = Path.Combine(rootPath, "baselines");
        }

        public string RootPath { get; }
        public string UploadsPath { get; }
        public string BaselinesPath { get; }
    }
}
