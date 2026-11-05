using System.Text;

namespace HopTracer.Core.Services;

public static class UnifiedDiff
{
    public static string Generate(string oldText, string newText, int contextLines = 3)
    {
        if (string.Equals(oldText, newText, StringComparison.Ordinal))
        {
            return string.Empty;
        }

        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);
        var diff = ComputeDiff(oldLines, newLines);
        return FormatUnifiedDiff(diff, oldLines, newLines, contextLines);
    }

    public static (int Added, int Removed) CountChanges(string oldText, string newText)
    {
        var oldLines = SplitLines(oldText);
        var newLines = SplitLines(newText);
        var diff = ComputeDiff(oldLines, newLines);
        
        int added = 0, removed = 0;
        foreach (var (op, _, _) in diff)
        {
            if (op == DiffOp.Insert) added++;
            else if (op == DiffOp.Delete) removed++;
        }
        
        return (added, removed);
    }

    private static string[] SplitLines(string text)
    {
        return text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
    }

    private static List<(DiffOp Op, int OldIndex, int NewIndex)> ComputeDiff(string[] oldLines, string[] newLines)
    {
        var lcs = LongestCommonSubsequence(oldLines, newLines);
        var result = new List<(DiffOp, int, int)>();
        
        int i = 0, j = 0, k = 0;
        while (i < oldLines.Length || j < newLines.Length)
        {
            if (k < lcs.Count && i == lcs[k].OldIndex && j == lcs[k].NewIndex)
            {
                result.Add((DiffOp.Equal, i, j));
                i++;
                j++;
                k++;
            }
            else if (k < lcs.Count && i < lcs[k].OldIndex && j < lcs[k].NewIndex)
            {
                result.Add((DiffOp.Delete, i, -1));
                i++;
            }
            else if (k < lcs.Count && j < lcs[k].NewIndex)
            {
                result.Add((DiffOp.Insert, -1, j));
                j++;
            }
            else if (i < oldLines.Length)
            {
                result.Add((DiffOp.Delete, i, -1));
                i++;
            }
            else
            {
                result.Add((DiffOp.Insert, -1, j));
                j++;
            }
        }
        
        return result;
    }

    private static List<(int OldIndex, int NewIndex)> LongestCommonSubsequence(string[] oldLines, string[] newLines)
    {
        int m = oldLines.Length, n = newLines.Length;
        var dp = new int[m + 1, n + 1];
        
        for (int i = 1; i <= m; i++)
        {
            for (int j = 1; j <= n; j++)
            {
                if (string.Equals(oldLines[i - 1], newLines[j - 1], StringComparison.Ordinal))
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                }
                else
                {
                    dp[i, j] = Math.Max(dp[i - 1, j], dp[i, j - 1]);
                }
            }
        }
        
        var lcs = new List<(int, int)>();
        int ii = m, jj = n;
        while (ii > 0 && jj > 0)
        {
            if (string.Equals(oldLines[ii - 1], newLines[jj - 1], StringComparison.Ordinal))
            {
                lcs.Insert(0, (ii - 1, jj - 1));
                ii--;
                jj--;
            }
            else if (dp[ii - 1, jj] > dp[ii, jj - 1])
            {
                ii--;
            }
            else
            {
                jj--;
            }
        }
        
        return lcs;
    }

    private static string FormatUnifiedDiff(
        List<(DiffOp Op, int OldIndex, int NewIndex)> diff,
        string[] oldLines,
        string[] newLines,
        int contextLines)
    {
        var sb = new StringBuilder();
        var hunks = BuildHunks(diff, contextLines);
        
        foreach (var hunk in hunks)
        {
            sb.AppendLine($"@@ -{hunk.OldStart + 1},{hunk.OldCount} +{hunk.NewStart + 1},{hunk.NewCount} @@");
            
            for (int i = hunk.StartIndex; i <= hunk.EndIndex; i++)
            {
                var (op, oldIdx, newIdx) = diff[i];
                switch (op)
                {
                    case DiffOp.Equal:
                        sb.AppendLine(" " + oldLines[oldIdx]);
                        break;
                    case DiffOp.Delete:
                        sb.AppendLine("-" + oldLines[oldIdx]);
                        break;
                    case DiffOp.Insert:
                        sb.AppendLine("+" + newLines[newIdx]);
                        break;
                }
            }
        }
        
        return sb.ToString().TrimEnd();
    }

    private static List<Hunk> BuildHunks(List<(DiffOp Op, int OldIndex, int NewIndex)> diff, int contextLines)
    {
        var hunks = new List<Hunk>();
        if (diff.Count == 0) return hunks;
        
        int? hunkStart = null;
        int lastChange = -1;
        
        for (int i = 0; i < diff.Count; i++)
        {
            if (diff[i].Op != DiffOp.Equal)
            {
                if (hunkStart == null)
                {
                    hunkStart = Math.Max(0, i - contextLines);
                }
                lastChange = i;
            }
            else if (hunkStart != null && i > lastChange + contextLines * 2)
            {
                hunks.Add(ComputeHunk(diff, hunkStart.Value, Math.Min(lastChange + contextLines, diff.Count - 1)));
                hunkStart = null;
            }
        }
        
        if (hunkStart != null)
        {
            hunks.Add(ComputeHunk(diff, hunkStart.Value, Math.Min(lastChange + contextLines, diff.Count - 1)));
        }
        
        return hunks;
    }

    private static Hunk ComputeHunk(List<(DiffOp Op, int OldIndex, int NewIndex)> diff, int start, int end)
    {
        int oldStart = -1, newStart = -1, oldCount = 0, newCount = 0;
        
        for (int i = start; i <= end; i++)
        {
            var (op, oldIdx, newIdx) = diff[i];
            if (op == DiffOp.Equal || op == DiffOp.Delete)
            {
                if (oldStart == -1) oldStart = oldIdx;
                oldCount++;
            }
            if (op == DiffOp.Equal || op == DiffOp.Insert)
            {
                if (newStart == -1) newStart = newIdx;
                newCount++;
            }
        }
        
        return new Hunk(start, end, oldStart, oldCount, newStart, newCount);
    }

    private enum DiffOp { Equal, Insert, Delete }
    
    private record Hunk(int StartIndex, int EndIndex, int OldStart, int OldCount, int NewStart, int NewCount);
}
