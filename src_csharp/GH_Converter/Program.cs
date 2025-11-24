using System;
using System.IO;
using GH_IO.Serialization;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine("Usage: GH_Converter <input.gh> <output.ghx>");
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
}
