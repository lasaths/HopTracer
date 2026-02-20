using HopTracer.Core.Models;
using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using TestDiff;

// Test the core diff functionality
Console.WriteLine("=== GH Diff Tool - Core Logic Test ===\n");

if (args.Length == 0)
{
    Console.WriteLine("Usage:");
    Console.WriteLine("  1. Compare two files:");
    Console.WriteLine(@"     TestDiff.exe <oldFile.ghx> <newFile.ghx>");
    Console.WriteLine("\n  2. Compare file with last Git commit:");
    Console.WriteLine(@"     TestDiff.exe --git <file.gh|ghx>");
    Console.WriteLine("\nExamples:");
    Console.WriteLine(@"  TestDiff.exe C:\path\to\old.ghx C:\path\to\new.ghx");
    Console.WriteLine(@"  TestDiff.exe --git C:\path\to\file.gh");
    return 1;
}

// Check if Git mode
if (args.Length == 2 && args[0] == "--git")
{
    var test = new GitDiffTest(args[1]);
    var success = await test.RunTest();
    return success ? 0 : 1;
}

if (args.Length < 2)
{
    Console.WriteLine("✗ ERROR: Not enough arguments");
    Console.WriteLine("Usage: TestDiff <oldFile.ghx> <newFile.ghx>");
    Console.WriteLine("   or: TestDiff --git <file.gh|ghx>");
    return 1;
}

var oldFile = args[0];
var newFile = args[1];

if (!File.Exists(oldFile))
{
    Console.WriteLine($"✗ ERROR: Old file not found: {oldFile}");
    return 1;
}

if (!File.Exists(newFile))
{
    Console.WriteLine($"✗ ERROR: New file not found: {newFile}");
    return 1;
}

Console.WriteLine($"OLD: {oldFile}");
Console.WriteLine($"NEW: {newFile}\n");

// Setup services
var parser = new GhxParser(NullLogger<GhxParser>.Instance);
var differ = new Differ(NullLogger<Differ>.Instance);

try
{
    Console.WriteLine("Parsing OLD file...");
    var graphOld = parser.Parse(oldFile);
    Console.WriteLine($"  ✓ Found {graphOld.Nodes.Count} nodes, {graphOld.Edges.Count} edges");

    Console.WriteLine("Parsing NEW file...");
    var graphNew = parser.Parse(newFile);
    Console.WriteLine($"  ✓ Found {graphNew.Nodes.Count} nodes, {graphNew.Edges.Count} edges\n");

    Console.WriteLine("Computing diff...");
    var (nodes, edges) = differ.Diff(graphOld, graphNew);

    var added = nodes.Count(n => n.Status == "added");
    var removed = nodes.Count(n => n.Status == "removed");
    var modified = nodes.Count(n => n.Status == "modified");
    var same = nodes.Count(n => n.Status == "same");

    var edgesAdded = edges.Count(e => e.Status == "added");
    var edgesRemoved = edges.Count(e => e.Status == "removed");

    Console.WriteLine($"  Node Summary:");
    Console.WriteLine($"    Added:    {added}");
    Console.WriteLine($"    Removed:  {removed}");
    Console.WriteLine($"    Modified: {modified}");
    Console.WriteLine($"    Same:     {same}");
    Console.WriteLine($"    TOTAL:    {nodes.Count}");

    Console.WriteLine($"\n  Edge Summary:");
    Console.WriteLine($"    Added:   {edgesAdded}");
    Console.WriteLine($"    Removed: {edgesRemoved}");
    Console.WriteLine($"    Same:    {edges.Count - edgesAdded - edgesRemoved}");
    Console.WriteLine($"    TOTAL:   {edges.Count}");

    Console.WriteLine($"\n✓ Diff computation successful!");

    // Show a few example changes
    Console.WriteLine($"\nExample changes (first 5):");
    var changes = nodes.Where(n => n.Status != "same").Take(5);
    foreach (var node in changes)
    {
        Console.WriteLine($"  - {node.Status.ToUpper()}: {node.Name} ({node.Nickname})");
    }

    Console.WriteLine($"\n=== TEST PASSED ===");
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ ERROR: {ex.Message}");
    Console.WriteLine($"  {ex.StackTrace}");
    Console.WriteLine($"\n=== TEST FAILED ===");
    return 1;
}
