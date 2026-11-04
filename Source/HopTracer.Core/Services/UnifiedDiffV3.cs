using System.Text;
using DiffPlex;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;

namespace HopTracer.Core.Services;

/// <summary>
/// Unified diff generator using DiffPlex library with 80-line cap per script.
/// </summary>
public static class UnifiedDiffV3
{
    private const int MaxDiffLines = 80;
    private const int ContextLines = 3;

    public static (int Added, int Removed, string Diff, bool Truncated, string? LineEndingChange) Generate(string oldText, string newText)
    {
        // Check for line ending changes
        var oldEnding = DetectLineEnding(oldText);
        var newEnding = DetectLineEnding(newText);
        string? lineEndingChange = null;
        
        if (oldEnding != newEnding && oldEnding != null && newEnding != null)
        {
            lineEndingChange = $"{oldEnding}→{newEnding}";
        }

        // Normalize line endings for diff
        var oldNorm = NormalizeLineEndings(oldText);
        var newNorm = NormalizeLineEndings(newText);

        // Use DiffPlex to compute the diff
        var diffBuilder = new InlineDiffBuilder(new DiffPlex.Differ());
        var diff = diffBuilder.BuildDiffModel(oldNorm, newNorm, ignoreWhitespace: false);

        int added = 0, removed = 0;
        var diffLines = new List<string>();
        bool truncated = false;

        // Build unified diff hunks
        var hunks = BuildHunks(diff.Lines);
        
        foreach (var hunk in hunks)
        {
            if (diffLines.Count >= MaxDiffLines)
            {
                truncated = true;
                break;
            }

            // Hunk header
            diffLines.Add($"@@ -{hunk.OldStart + 1},{hunk.OldCount} +{hunk.NewStart + 1},{hunk.NewCount} @@");
            
            // Hunk content
            foreach (var line in hunk.Lines)
            {
                if (diffLines.Count >= MaxDiffLines)
                {
                    truncated = true;
                    break;
                }

                switch (line.Type)
                {
                    case ChangeType.Inserted:
                        diffLines.Add("+" + line.Text);
                        added++;
                        break;
                    case ChangeType.Deleted:
                        diffLines.Add("-" + line.Text);
                        removed++;
                        break;
                    case ChangeType.Unchanged:
                    case ChangeType.Imaginary:
                        diffLines.Add(" " + line.Text);
                        break;
                    case ChangeType.Modified:
                        // DiffPlex shows these as delete + insert
                        diffLines.Add("-" + line.Text);
                        removed++;
                        break;
                }
            }

            if (truncated) break;
        }

        return (added, removed, string.Join("\n", diffLines), truncated, lineEndingChange);
    }

    private static string? DetectLineEnding(string text)
    {
        if (text.Contains("\r\n")) return "CRLF";
        if (text.Contains("\n")) return "LF";
        if (text.Contains("\r")) return "CR";
        return null;
    }

    private static string NormalizeLineEndings(string text)
    {
        return text.Replace("\r\n", "\n").Replace("\r", "\n");
    }

    private static List<Hunk> BuildHunks(List<DiffPiece> lines)
    {
        var hunks = new List<Hunk>();
        if (lines.Count == 0) return hunks;

        int? hunkStart = null;
        int lastChange = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            
            if (line.Type != ChangeType.Unchanged && line.Type != ChangeType.Imaginary)
            {
                if (hunkStart == null)
                {
                    // Start new hunk with context
                    hunkStart = Math.Max(0, i - ContextLines);
                }
                lastChange = i;
            }
            else if (hunkStart != null && i > lastChange + ContextLines * 2)
            {
                // End current hunk with context
                hunks.Add(ComputeHunk(lines, hunkStart.Value, Math.Min(lastChange + ContextLines, lines.Count - 1)));
                hunkStart = null;
            }
        }

        // Final hunk
        if (hunkStart != null)
        {
            hunks.Add(ComputeHunk(lines, hunkStart.Value, Math.Min(lastChange + ContextLines, lines.Count - 1)));
        }

        return hunks;
    }

    private static Hunk ComputeHunk(List<DiffPiece> lines, int start, int end)
    {
        int oldStart = 0, newStart = 0, oldCount = 0, newCount = 0;
        var hunkLines = new List<DiffPiece>();

        // Calculate positions
        for (int i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            
            if (i < start)
            {
                // Count lines before hunk
                if (line.Type != ChangeType.Inserted)
                    oldStart++;
                if (line.Type != ChangeType.Deleted)
                    newStart++;
            }
            else if (i >= start && i <= end)
            {
                // Lines in hunk
                hunkLines.Add(line);
                
                if (line.Type != ChangeType.Inserted)
                    oldCount++;
                if (line.Type != ChangeType.Deleted)
                    newCount++;
            }
        }

        return new Hunk(oldStart, oldCount, newStart, newCount, hunkLines);
    }

    private record Hunk(int OldStart, int OldCount, int NewStart, int NewCount, List<DiffPiece> Lines);
}
