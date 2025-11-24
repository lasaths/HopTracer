using HopTracer.Core.Models;
using HopTracer.Core.Services;

// Test the core diff functionality
Console.WriteLine("=== GH Diff Tool - Core Logic Test ===\n");

var oldFile = @"C:\Users\lasaths\Downloads\comptest\DataDrop16.ghx";
var newFile = @"C:\Users\lasaths\Downloads\comptest\DataDrop17.ghx";

Console.WriteLine($"OLD: {oldFile}");
Console.WriteLine($"NEW: {newFile}\n");

try
{
    Console.WriteLine("Parsing OLD file...");
    var graphOld = GhxParser.Parse(oldFile);
    Console.WriteLine($"  ✓ Found {graphOld.Nodes.Count} nodes, {graphOld.Edges.Count} edges");

    Console.WriteLine("Parsing NEW file...");
    var graphNew = GhxParser.Parse(newFile);
    Console.WriteLine($"  ✓ Found {graphNew.Nodes.Count} nodes, {graphNew.Edges.Count} edges\n");

    Console.WriteLine("Computing diff...");
    var (nodes, edges) = Differ.Diff(graphOld, graphNew);

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
