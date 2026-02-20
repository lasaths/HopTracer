// Quick script to convert .gh to .ghx using GH_IO
using GH_IO.Serialization;
using System.IO;

var baseDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "Tests", "data"));
var ghFiles = Directory.GetFiles(baseDir, "*.gh");

foreach (var f in ghFiles)
{
    Console.WriteLine($"Converting: {f}");
    var archive = new GH_Archive();
    if (archive.ReadFromFile(f))
    {
        var outPath = Path.ChangeExtension(f, ".converted.ghx");
        archive.WriteToFile(outPath, true, false);
        Console.WriteLine($"  -> {outPath}");
    }
    else
    {
        Console.WriteLine($"  FAILED to read!");
    }
}
