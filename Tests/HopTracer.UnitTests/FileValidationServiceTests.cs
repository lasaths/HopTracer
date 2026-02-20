using HopTracer.Web.Services;
using Xunit;

namespace HopTracer.UnitTests;

public class FileValidationServiceTests : IDisposable
{
    private readonly FileValidationService _validator = new();
    private readonly List<string> _tempFiles = new();

    [Fact]
    public void TryValidateExistingPath_ReturnsNormalizedPath_ForValidFile()
    {
        var path = CreateTempFile(".ghx");
        var result = _validator.TryValidateExistingPath(path, out var error, out var normalized);

        Assert.True(result);
        Assert.True(Path.IsPathRooted(normalized));
        Assert.Equal(string.Empty, error);
    }

    [Fact]
    public void TryValidateExistingPath_RejectsDisallowedExtension()
    {
        var path = CreateTempFile(".txt");
        var result = _validator.TryValidateExistingPath(path, out var error, out var _);

        Assert.False(result);
        Assert.Contains("Invalid file type", error);
    }

    [Fact]
    public void SanitizeFileName_StripsUnsafeCharactersAndEnforcesExtension()
    {
        var sanitized = _validator.SanitizeFileName(@"..\\unsafe?name.txt");

        Assert.DoesNotContain("?", sanitized);
        Assert.DoesNotContain("..", sanitized);
        Assert.EndsWith(".ghx", sanitized, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    private string CreateTempFile(string extension)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hoptracer_{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, "test");
        _tempFiles.Add(path);
        return path;
    }
}
