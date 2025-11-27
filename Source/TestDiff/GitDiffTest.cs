using HopTracer.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TestDiff;

public class GitDiffTest
{
    private readonly string _testFilePath;
    private readonly ILogger<ConverterService> _converterLogger;
    private readonly ILogger<GitWrapper> _gitLogger;
    private readonly ILogger<GhxParser> _parserLogger;
    private readonly ILogger<Differ> _differLogger;

    public GitDiffTest(string testFilePath)
    {
        _testFilePath = testFilePath;
        _converterLogger = NullLogger<ConverterService>.Instance;
        _gitLogger = NullLogger<GitWrapper>.Instance;
        _parserLogger = NullLogger<GhxParser>.Instance;
        _differLogger = NullLogger<Differ>.Instance;
    }

    public async Task<bool> RunTest()
    {
        Console.WriteLine("=== Git Diff Integration Test ===\n");
        Console.WriteLine($"Target file: {_testFilePath}\n");

        if (!File.Exists(_testFilePath))
        {
            Console.WriteLine($"✗ ERROR: File not found: {_testFilePath}");
            return false;
        }

        try
        {
            // Step 1: Convert .gh to .ghx if needed
            Console.WriteLine("Step 1: Converting .gh to .ghx (if needed)...");
            var converter = new ConverterService(_converterLogger);
            var currentGhxPath = ConvertIfNeeded(_testFilePath, converter);
            Console.WriteLine($"  ✓ Current file: {currentGhxPath}\n");

            // Step 2: Get Git commits
            Console.WriteLine("Step 2: Getting Git history...");
            var gitWrapper = new GitWrapper(_gitLogger);
            var commits = gitWrapper.GetCommits(_testFilePath);
            
            if (commits == null || commits.Count == 0)
            {
                Console.WriteLine("  ✗ No Git history found for this file");
                return false;
            }

            Console.WriteLine($"  ✓ Found {commits.Count} commits");
            var lastCommit = commits[0]; // Most recent commit
            var hashDisplay = lastCommit.Hash.Length > 8 ? lastCommit.Hash[..8] : lastCommit.Hash;
            Console.WriteLine($"  Last commit: {hashDisplay} - {lastCommit.Message}");
            Console.WriteLine($"  Author: {lastCommit.Author}");
            Console.WriteLine($"  Date: {lastCommit.Date}\n");

            // Step 3: Get the file from the last commit
            Console.WriteLine("Step 3: Retrieving file from last commit...");
            var oldFileBytes = gitWrapper.GetFileContentAtCommit(lastCommit.Hash, _testFilePath);
            
            if (oldFileBytes == null || oldFileBytes.Length == 0)
            {
                Console.WriteLine("  ✗ Could not retrieve file from commit");
                return false;
            }

            // Save to temp file
            var tempOldPath = Path.Combine(Path.GetTempPath(), $"old_{Path.GetFileName(_testFilePath)}");
            await File.WriteAllBytesAsync(tempOldPath, oldFileBytes);
            Console.WriteLine($"  ✓ Old version saved to: {tempOldPath}");

            // Convert old file if it's .gh
            var oldGhxPath = ConvertIfNeeded(tempOldPath, converter);
            Console.WriteLine($"  ✓ Old file ready: {oldGhxPath}\n");

            // Step 4: Parse both files
            Console.WriteLine("Step 4: Parsing Grasshopper files...");
            var parser = new GhxParser(_parserLogger);
            
            Console.WriteLine("  Parsing OLD version...");
            var graphOld = parser.Parse(oldGhxPath);
            Console.WriteLine($"    ✓ Found {graphOld.Nodes.Count} nodes, {graphOld.Edges.Count} edges");

            Console.WriteLine("  Parsing NEW version...");
            var graphNew = parser.Parse(currentGhxPath);
            Console.WriteLine($"    ✓ Found {graphNew.Nodes.Count} nodes, {graphNew.Edges.Count} edges\n");

            // Step 5: Compute diff
            Console.WriteLine("Step 5: Computing diff...");
            var differ = new Differ(_differLogger);
            var (nodes, edges) = differ.Diff(graphOld, graphNew);

            var added = nodes.Count(n => n.Status == "added");
            var removed = nodes.Count(n => n.Status == "removed");
            var modified = nodes.Count(n => n.Status == "modified");
            var same = nodes.Count(n => n.Status == "same");

            var edgesAdded = edges.Count(e => e.Status == "added");
            var edgesRemoved = edges.Count(e => e.Status == "removed");
            var edgesSame = edges.Count(e => e.Status == "same");

            Console.WriteLine($"  Node Summary:");
            Console.WriteLine($"    Added:    {added}");
            Console.WriteLine($"    Removed:  {removed}");
            Console.WriteLine($"    Modified: {modified}");
            Console.WriteLine($"    Same:     {same}");
            Console.WriteLine($"    TOTAL:    {nodes.Count}");

            Console.WriteLine($"\n  Edge Summary:");
            Console.WriteLine($"    Added:   {edgesAdded}");
            Console.WriteLine($"    Removed: {edgesRemoved}");
            Console.WriteLine($"    Same:    {edgesSame}");
            Console.WriteLine($"    TOTAL:   {edges.Count}\n");

            // Step 6: Show detailed changes
            Console.WriteLine("Step 6: Change Details:");
            
            if (added > 0)
            {
                Console.WriteLine($"\n  Added Nodes ({added}):");
                var addedNodes = nodes.Where(n => n.Status == "added").Take(10);
                foreach (var node in addedNodes)
                {
                    Console.WriteLine($"    + {node.Name} ({node.Nickname}) at ({node.X:F0}, {node.Y:F0})");
                }
                if (added > 10) Console.WriteLine($"    ... and {added - 10} more");
            }

            if (removed > 0)
            {
                Console.WriteLine($"\n  Removed Nodes ({removed}):");
                var removedNodes = nodes.Where(n => n.Status == "removed").Take(10);
                foreach (var node in removedNodes)
                {
                    Console.WriteLine($"    - {node.Name} ({node.Nickname}) at ({node.X:F0}, {node.Y:F0})");
                }
                if (removed > 10) Console.WriteLine($"    ... and {removed - 10} more");
            }

            if (modified > 0)
            {
                Console.WriteLine($"\n  Modified Nodes ({modified}):");
                var modifiedNodes = nodes.Where(n => n.Status == "modified").Take(10);
                foreach (var node in modifiedNodes)
                {
                    Console.WriteLine($"    ~ {node.Name} ({node.Nickname}) at ({node.X:F0}, {node.Y:F0})");
                }
                if (modified > 10) Console.WriteLine($"    ... and {modified - 10} more");
            }

            // Cleanup
            try
            {
                if (File.Exists(tempOldPath)) File.Delete(tempOldPath);
                if (oldGhxPath != tempOldPath && File.Exists(oldGhxPath)) File.Delete(oldGhxPath);
            }
            catch { /* Ignore cleanup errors */ }

            Console.WriteLine($"\n=== TEST PASSED ===");
            var finalHash = lastCommit.Hash.Length > 8 ? lastCommit.Hash[..8] : lastCommit.Hash;
            Console.WriteLine($"Successfully compared current file with last commit ({finalHash})");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ ERROR: {ex.Message}");
            Console.WriteLine($"  Type: {ex.GetType().Name}");
            Console.WriteLine($"  Stack: {ex.StackTrace}");
            Console.WriteLine($"\n=== TEST FAILED ===");
            return false;
        }
    }

    private string ConvertIfNeeded(string filePath, ConverterService converter)
    {
        if (filePath.EndsWith(".ghx", StringComparison.OrdinalIgnoreCase))
        {
            return filePath;
        }

        if (filePath.EndsWith(".gh", StringComparison.OrdinalIgnoreCase))
        {
            var ghxPath = Path.ChangeExtension(filePath, ".ghx");

            // If a sibling GHX already exists, prefer it to avoid converter dependency.
            if (File.Exists(ghxPath))
            {
                Console.WriteLine($"  Using existing GHX: {ghxPath}");
                return ghxPath;
            }
            
            // Use GhConverter.exe instead of ConverterService
            var ghConverterPath = Path.Combine(
                AppContext.BaseDirectory, 
                "..", "..", "..", "..", 
                "GhConverter", "publish", "GhConverter.exe");
            
            ghConverterPath = Path.GetFullPath(ghConverterPath);
            
            if (!File.Exists(ghConverterPath))
            {
                throw new FileNotFoundException($"GhConverter.exe not found at {ghConverterPath}. Please build GhConverter project.");
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ghConverterPath,
                Arguments = $"\"{filePath}\" \"{ghxPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start GhConverter process");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"GhConverter failed: {error}");
            }

            if (!File.Exists(ghxPath))
            {
                throw new InvalidOperationException($"Conversion failed - output file not created: {ghxPath}");
            }

            return ghxPath;
        }

        throw new InvalidOperationException($"Unsupported file type: {filePath}");
    }
}
