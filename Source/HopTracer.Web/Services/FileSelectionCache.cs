using System.IO;
using System.Threading;

namespace HopTracer.Web.Services;

public interface IFileSelectionCache
{
    void Record(string path);
    bool TryGet(out string? path);
}

public class FileSelectionCache : IFileSelectionCache
{
    private readonly ReaderWriterLockSlim _lock = new();
    private string? _lastPath;

    public void Record(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var normalizedPath = Normalize(path);
        _lock.EnterWriteLock();
        try
        {
            _lastPath = normalizedPath;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool TryGet(out string? path)
    {
        _lock.EnterReadLock();
        try
        {
            path = _lastPath;
            return !string.IsNullOrEmpty(path);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }
}
