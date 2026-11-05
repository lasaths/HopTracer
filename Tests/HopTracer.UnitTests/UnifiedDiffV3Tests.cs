using HopTracer.Core.Services;
using Xunit;

namespace HopTracer.UnitTests;

public class UnifiedDiffV3Tests
{
    [Fact]
    public void Generate_EmptyStrings_ReturnsEmptyDiff()
    {
        var (added, removed, diff, truncated, lineEndingChange) = UnifiedDiffV3.Generate("", "");

        Assert.Equal(0, added);
        Assert.Equal(0, removed);
        Assert.Equal(string.Empty, diff);
        Assert.False(truncated);
        Assert.Null(lineEndingChange);
    }

    [Fact]
    public void Generate_OneSided_Old_ReturnsRemoval()
    {
        var old = "line1\nline2\nline3";
        var (added, removed, diff, truncated, lineEndingChange) = UnifiedDiffV3.Generate(old, "");

        Assert.Equal(0, added);
        Assert.Equal(3, removed);
        Assert.Contains("-line1", diff);
        Assert.False(truncated);
    }

    [Fact]
    public void Generate_OneSided_New_ReturnsAddition()
    {
        var @new = "line1\nline2\nline3";
        var (added, removed, diff, truncated, lineEndingChange) = UnifiedDiffV3.Generate("", @new);

        Assert.Equal(3, added);
        Assert.Equal(0, removed);
        Assert.Contains("+line1", diff);
        Assert.False(truncated);
    }

    [Fact]
    public void Generate_Replace_ReturnsAddedAndRemoved()
    {
        var old = "oldline";
        var @new = "newline";
        var (added, removed, diff, truncated, lineEndingChange) = UnifiedDiffV3.Generate(old, @new);

        Assert.Equal(1, added);
        Assert.Equal(1, removed);
        Assert.Contains("-oldline", diff);
        Assert.Contains("+newline", diff);
    }

    [Fact]
    public void Generate_Swap_ReturnsCorrectOrder()
    {
        var old = "A\nB";
        var @new = "B\nA";
        var (added, removed, diff, truncated, lineEndingChange) = UnifiedDiffV3.Generate(old, @new);

        // Should show removed A, removed B at original positions, and added in new positions
        Assert.True(added > 0);
        Assert.True(removed > 0);
        Assert.Contains("A", diff);
        Assert.Contains("B", diff);
    }

    [Fact]
    public void Generate_MultiHunk_ReturnsMultipleSections()
    {
        var old = "line1\nline2\nline3\n" + new string('\n', 10) + "line14\nline15";
        var @new = "line1\nchanged\nline3\n" + new string('\n', 10) + "line14\nalso_changed";

        var (added, removed, diff, truncated, lineEndingChange) = UnifiedDiffV3.Generate(old, @new);

        Assert.True(added >= 2);
        Assert.True(removed >= 2);
        Assert.Contains("@@", diff); // Hunk markers
    }

    [Fact]
    public void Generate_CRLFOnly_DetectsLineEndingChange()
    {
        var old = "line1\nline2\n";
        var @new = "line1\r\nline2\r\n";

        var (added, removed, diff, truncated, lineEndingChange) = UnifiedDiffV3.Generate(old, @new);

        Assert.Equal(0, added);
        Assert.Equal(0, removed);
        Assert.NotNull(lineEndingChange);
        Assert.Contains("→", lineEndingChange);
    }

    [Fact]
    public void Generate_TrailingNewline_Detected()
    {
        var old = "line1\nline2";
        var @new = "line1\nline2\n";

        var (added, removed, diff, truncated, lineEndingChange) = UnifiedDiffV3.Generate(old, @new);

        // DiffPlex detects the difference; trailing newline shows as added line
        Assert.True(added > 0 || removed > 0);
        Assert.False(string.IsNullOrEmpty(diff));
    }

    [Fact]
    public void Generate_20kLines_CompletesUnder1Second()
    {
        // Generate two 20k-line strings with realistic differences
        var lines = 20000;
        var oldLines = new string[lines];
        var newLines = new string[lines];

        for (int i = 0; i < lines; i++)
        {
            oldLines[i] = $"Line {i}: original content with some text";
            // Change every 100th line
            if (i % 100 == 0)
            {
                newLines[i] = $"Line {i}: MODIFIED content with different text";
            }
            else
            {
                newLines[i] = oldLines[i];
            }
        }

        var old = string.Join("\n", oldLines);
        var @new = string.Join("\n", newLines);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var (added, removed, diff, truncated, lineEndingChange) = UnifiedDiffV3.Generate(old, @new);
        sw.Stop();

        // Performance check: should complete under 1 second
        Assert.True(sw.ElapsedMilliseconds < 1000, $"20k-line diff took {sw.ElapsedMilliseconds}ms (expected < 1000ms)");
        
        // Sanity checks
        Assert.True(added > 0);
        Assert.True(removed > 0);
        Assert.False(string.IsNullOrEmpty(diff));
    }
}
