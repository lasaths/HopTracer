using Microsoft.AspNetCore.Http;

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HopTracer.Web.Services;

public interface IFileValidationService
{
    IReadOnlyCollection<string> AllowedExtensions { get; }
    bool TryValidateExistingPath(string? path, out string error, out string normalizedPath);
    bool TryValidateUpload(IFormFile? file, long maxSize, out string error);
    string SanitizeFileName(string fileName);
}

public class FileValidationService : IFileValidationService
{
    private static readonly string[] ExtensionList = { ".gh", ".ghx" };
    private static readonly HashSet<string> Extensions = new(ExtensionList, StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> AllowedExtensions => ExtensionList;

    public bool TryValidateExistingPath(string? path, out string error, out string normalizedPath)
    {
        error = string.Empty;
        normalizedPath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "File path is required";
            return false;
        }

        if (path.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            error = "Path contains invalid characters";
            return false;
        }

        try
        {
            normalizedPath = Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            error = $"Invalid file path: {ex.Message}";
            return false;
        }

        if (!File.Exists(normalizedPath))
        {
            error = $"File not found: {Path.GetFileName(normalizedPath)}";
            return false;
        }

        if (!Extensions.Contains(Path.GetExtension(normalizedPath)))
        {
            error = $"Invalid file type. Allowed: {string.Join(", ", ExtensionList)}";
            return false;
        }

        return true;
    }

    public bool TryValidateUpload(IFormFile? file, long maxSize, out string error)
    {
        error = string.Empty;

        if (file == null)
        {
            error = "No file provided";
            return false;
        }

        if (file.Length <= 0)
        {
            error = "Uploaded file is empty";
            return false;
        }

        if (file.Length > maxSize)
        {
            error = $"File too large. Maximum size: {maxSize / (1024 * 1024)}MB";
            return false;
        }

        if (!Extensions.Contains(Path.GetExtension(file.FileName)))
        {
            error = $"Invalid file type. Allowed: {string.Join(", ", ExtensionList)}";
            return false;
        }

        return true;
    }

    public string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "upload.ghx" : fileName);
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }

        var extension = Path.GetExtension(name);
        if (!Extensions.Contains(extension))
        {
            extension = ".ghx";
        }

        var baseName = Path.GetFileNameWithoutExtension(name);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "upload";
        }

        return $"{baseName}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}";
    }
}
