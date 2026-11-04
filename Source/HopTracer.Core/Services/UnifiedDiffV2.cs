using System.Text;

namespace HopTracer.Core.Services;

/// <summary>
/// Unified diff generator using Myers algorithm with linear space complexity.
/// </summary>
public static class UnifiedDiffV2
{
    private const int MaxDiffLines = 2000;
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

        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);

        // If they're identical (including line-ending-only changes), return empty diff
        if (oldLines.SequenceEqual(newLines))
        {
            return (0, 0, string.Empty, false, lineEndingChange);
        }

        // Compute edit script using Myers algorithm
        var edits = MyersDiff(oldLines, newLines);
        
        int added = 0, removed = 0;
        foreach (var (op, _, _) in edits)
        {
            if (op == EditOp.Insert) added++;
            else if (op == EditOp.Delete) removed++;
        }

        // Generate unified diff format
        var (diff, truncated) = FormatUnifiedDiff(oldLines, newLines, edits);
        
        return (added, removed, diff, truncated, lineEndingChange);
    }

    private static string? DetectLineEnding(string text)
    {
        if (text.Contains("\r\n")) return "CRLF";
        if (text.Contains("\n")) return "LF";
        if (text.Contains("\r")) return "CR";
        return null;
    }

    private static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }

        return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }

    private static List<(EditOp Op, int OldLine, int NewLine)> MyersDiff(string[] a, string[] b)
    {
        int n = a.Length;
        int m = b.Length;
        int max = n + m;

        var v = new int[2 * max + 1];
        var trace = new List<int[]>();

        for (int d = 0; d <= max; d++)
        {
            for (int k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v[k - 1 + max] < v[k + 1 + max]))
                {
                    x = v[k + 1 + max];
                }
                else
                {
                    x = v[k - 1 + max] + 1;
                }

                int y = x - k;

                while (x < n && y < m && string.Equals(a[x], b[y], StringComparison.Ordinal))
                {
                    x++;
                    y++;
                }

                v[k + max] = x;

                if (x >= n && y >= m)
                {
                    return Backtrack(a, b, trace, v, k, d, max);
                }
            }

            trace.Add((int[])v.Clone());
        }

        return new List<(EditOp, int, int)>();
    }

    private static List<(EditOp Op, int OldLine, int NewLine)> Backtrack(
        string[] a, string[] b, List<int[]> trace, int[] v, int k, int d, int max)
    {
        int x = a.Length;
        int y = b.Length;
        var edits = new List<(EditOp, int, int)>();

        for (int depth = d; depth >= 0; depth--)
        {
            var vData = depth == d ? v : trace[depth];
            int oldK = k;

            if (k == -depth || (k != depth && vData[k - 1 + max] < vData[k + 1 + max]))
            {
                k = k + 1;
            }
            else
            {
                k = k - 1;
            }

            int prevX = depth > 0 ? vData[k + max] : 0;
            int prevY = prevX - k;

            while (x > prevX && y > prevY)
            {
                edits.Add((EditOp.Equal, x - 1, y - 1));
                x--;
                y--;
            }

            if (depth > 0)
            {
                if (x == prevX)
                {
                    edits.Add((EditOp.Insert, -1, y - 1));
                    y--;
                }
                else
                {
                    edits.Add((EditOp.Delete, x - 1, -1));
                    x--;
                }
            }

            k = oldK;
        }

        edits.Reverse();
        return edits;
    }

    private static (string Diff, bool Truncated) FormatUnifiedDiff(
        string[] oldLines, string[] newLines, List<(EditOp Op, int OldLine, int NewLine)> edits)
    {
        if (edits.Count == 0)
        {
            return (string.Empty, false);
        }

        var hunks = BuildHunks(edits, oldLines.Length, newLines.Length);
        var sb = new StringBuilder();
        int totalLines = 0;
        bool truncated = false;

        foreach (var hunk in hunks)
        {
            if (totalLines >= MaxDiffLines)
            {
                truncated = true;
                break;
            }

            sb.AppendLine($"@@ -{hunk.OldStart + 1},{hunk.OldCount} +{hunk.NewStart + 1},{hunk.NewCount} @@");
            totalLines++;

            for (int i = hunk.StartIndex; i <= hunk.EndIndex && totalLines < MaxDiffLines; i++)
            {
                var (op, oldIdx, newIdx) = edits[i];
                switch (op)
                {
                    case EditOp.Equal:
                        sb.AppendLine(" " + oldLines[oldIdx]);
                        break;
                    case EditOp.Delete:
                        sb.AppendLine("-" + oldLines[oldIdx]);
                        break;
                    case EditOp.Insert:
                        sb.AppendLine("+" + newLines[newIdx]);
                        break;
                }
                totalLines++;
            }
        }

        return (sb.ToString().TrimEnd(), truncated);
    }

    private static List<Hunk> BuildHunks(List<(EditOp Op, int OldLine, int NewLine)> edits, int oldLen, int newLen)
    {
        var hunks = new List<Hunk>();
        if (edits.Count == 0) return hunks;

        int? hunkStart = null;
        int lastChange = -1;

        for (int i = 0; i < edits.Count; i++)
        {
            if (edits[i].Op != EditOp.Equal)
            {
                if (hunkStart == null)
                {
                    hunkStart = Math.Max(0, i - ContextLines);
                }
                lastChange = i;
            }
            else if (hunkStart != null && i > lastChange + ContextLines * 2)
            {
                hunks.Add(ComputeHunk(edits, hunkStart.Value, Math.Min(lastChange + ContextLines, edits.Count - 1)));
                hunkStart = null;
            }
        }

        if (hunkStart != null)
        {
            hunks.Add(ComputeHunk(edits, hunkStart.Value, Math.Min(lastChange + ContextLines, edits.Count - 1)));
        }

        return hunks;
    }

    private static Hunk ComputeHunk(List<(EditOp Op, int OldLine, int NewLine)> edits, int start, int end)
    {
        int oldStart = -1, newStart = -1, oldCount = 0, newCount = 0;

        for (int i = start; i <= end; i++)
        {
            var (op, oldIdx, newIdx) = edits[i];
            if (op == EditOp.Equal || op == EditOp.Delete)
            {
                if (oldStart == -1) oldStart = oldIdx;
                oldCount++;
            }
            if (op == EditOp.Equal || op == EditOp.Insert)
            {
                if (newStart == -1) newStart = newIdx;
                newCount++;
            }
        }

        // Handle empty ranges
        if (oldStart == -1) oldStart = 0;
        if (newStart == -1) newStart = 0;

        return new Hunk(start, end, oldStart, oldCount, newStart, newCount);
    }

    private enum EditOp { Equal, Insert, Delete }

    private record Hunk(int StartIndex, int EndIndex, int OldStart, int OldCount, int NewStart, int NewCount);
}
