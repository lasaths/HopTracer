using System;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using GH_IO.Serialization;

class Program
{
    static void Main(string[] args)
    {
        RegisterAssemblyResolver();

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: GhConverter <input.gh> <output.ghx>");
            Environment.Exit(1);
        }

        string inputPath = args[0];
        string outputPath = args[1];

        if (!File.Exists(inputPath))
        {
            Console.WriteLine($"Error: Input file not found: {inputPath}");
            Environment.Exit(1);
        }

        try
        {
            var archive = new GH_Archive();
            if (!archive.ReadFromFile(inputPath))
            {
                Console.WriteLine("Error: Failed to read GH file.");
                Environment.Exit(1);
            }

            if (!archive.WriteToFile(outputPath, true, false)) // true = xml, false = binary
            {
                Console.WriteLine("Error: Failed to write GHX file.");
                Environment.Exit(1);
            }

            Console.WriteLine("Conversion successful.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void RegisterAssemblyResolver()
    {
        var baseDir = AppContext.BaseDirectory;
        var probeDirs = new[]
        {
            baseDir,
            Path.GetFullPath(Path.Combine(baseDir, "..")),
            Path.GetFullPath(Path.Combine(baseDir, "..", "publish"))
        }
        .Where(Directory.Exists)
        .Distinct()
        .ToArray();

        AssemblyLoadContext.Default.Resolving += (ctx, name) =>
        {
            var simple = name.Name;
            if (string.IsNullOrWhiteSpace(simple)) return null;

            var targets = new[] { "GH_IO", "GH_Util", "Grasshopper", "RhinoCommon", "Rhino.UI" };
            if (!targets.Contains(simple, StringComparer.OrdinalIgnoreCase)) return null;

            foreach (var dir in probeDirs)
            {
                var candidate = Path.Combine(dir, $"{simple}.dll");
                if (File.Exists(candidate))
                {
                    try
                    {
                        return ctx.LoadFromAssemblyPath(candidate);
                    }
                    catch
                    {
                        // continue probing
                    }
                }
            }
            return null;
        };
    }
}
