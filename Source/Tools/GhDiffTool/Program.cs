using HopTracer.Core.Models;
using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    PrintHelp(args.Length > 1 ? args[1] : null);
    Environment.Exit(0);
}

var command = args[0].ToLowerInvariant();
var (flags, positional) = ParseArgs(args.Skip(1).ToArray());

try
{
    var exitCode = command switch
    {
        "compare" => RunCompare(positional, flags),
        "git" => RunGit(positional, flags),
        _ => Fail($"Unknown command: {command}")
    };
    Environment.Exit(exitCode);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {GetDeepMessage(ex)}");
    Environment.Exit(1);
}

static string GetDeepMessage(Exception ex)
{
    while (ex.InnerException != null) ex = ex.InnerException;
    return ex.Message;
}

static int RunCompare(string[] positional, CliFlags flags)
{
    if (positional.Length < 2)
    {
        return Fail("Usage: hoptracer compare <oldFile> <newFile> [options]");
    }

    var oldPath = Path.GetFullPath(positional[0]);
    var newPath = Path.GetFullPath(positional[1]);
    if (!File.Exists(oldPath)) return Fail($"File not found: {oldPath}");
    if (!File.Exists(newPath)) return Fail($"File not found: {newPath}");

    var diff = ComputeDiff(oldPath, newPath);
    return WriteOutput(diff, oldPath, newPath, flags);
}

static int RunGit(string[] positional, CliFlags flags)
{
    if (positional.Length < 1)
    {
        return Fail("Usage: hoptracer git <file> [--commit <hash>] [options]");
    }

    var filePath = Path.GetFullPath(positional[0]);
    if (!File.Exists(filePath)) return Fail($"File not found: {filePath}");

    var dir = Path.GetDirectoryName(filePath) ?? ".";
    var git = new GitWrapper(dir, NullLogger<GitWrapper>.Instance);
    if (!git.IsGitRepo())
    {
        return Fail($"Not a git repository: {dir}");
    }

    var commitHash = flags.Commit;
    if (string.IsNullOrWhiteSpace(commitHash))
    {
        var commits = git.GetCommits(filePath, 1);
        if (commits.Count == 0)
        {
            return Fail($"No git history found for: {filePath}");
        }
        commitHash = commits[0].Hash;
    }

    var ext = Path.GetExtension(filePath);
    var oldTemp = Path.Combine(Path.GetTempPath(), $"hoptracer-git-{Guid.NewGuid():N}{ext}");
    try
    {
        File.WriteAllBytes(oldTemp, git.GetFileContentAtCommit(commitHash, filePath));
        var diff = ComputeDiff(oldTemp, filePath);
        flags.FileOldLabel = $"{Path.GetFileName(filePath)}@{commitHash[..Math.Min(7, commitHash.Length)]}";
        flags.FileNewLabel = Path.GetFileName(filePath);
        return WriteOutput(diff, flags.FileOldLabel, flags.FileNewLabel, flags);
    }
    finally
    {
        TryDelete(oldTemp);
    }
}

static DiffComputation ComputeDiff(string oldPath, string newPath)
{
    var parser = new GhxParser(NullLogger<GhxParser>.Instance);
    var differ = new Differ(NullLogger<Differ>.Instance);
    var converter = new ConverterService(NullLogger<ConverterService>.Instance);
    var temps = new List<string>();

    try
    {
        var preparedOld = PreparePath(oldPath, converter, temps);
        var preparedNew = PreparePath(newPath, converter, temps);
        return differ.DiffDetailed(parser.Parse(preparedOld), parser.Parse(preparedNew));
    }
    finally
    {
        foreach (var temp in temps) TryDelete(temp);
    }
}

static string PreparePath(string path, ConverterService converter, List<string> temps)
{
    if (!path.EndsWith(".gh", StringComparison.OrdinalIgnoreCase))
    {
        return path;
    }

    if (!converter.TryCheckDependencies(out var message))
    {
        throw new InvalidOperationException(message);
    }

    var tmp = Path.Combine(Path.GetTempPath(), $"hoptracer-{Guid.NewGuid():N}.ghx");
    temps.Add(tmp);
    return converter.ConvertGhToGhx(path, tmp);
}

static int WriteOutput(DiffComputation diff, string fileOld, string fileNew, CliFlags flags)
{
    var options = flags.ToOptions(fileOld, fileNew);
    var output = new DiffOutputGenerator();
    var text = output.Generate(diff, options, flags.Format);

    if (!string.IsNullOrWhiteSpace(flags.OutputPath))
    {
        File.WriteAllText(flags.OutputPath, text);
    }
    else
    {
        Console.Write(text);
    }

    if (flags.FailOnRisk && diff.RiskSummary.CriticalCount + diff.RiskSummary.HighCount > 0)
    {
        return 1;
    }

    return 0;
}

static void TryDelete(string path)
{
    try { if (File.Exists(path)) File.Delete(path); } catch { /* ponytail: best-effort temp cleanup */ }
}

static int Fail(string message)
{
    Console.Error.WriteLine(message);
    return 1;
}

static (CliFlags Flags, string[] Positional) ParseArgs(string[] args)
{
    var flags = new CliFlags();
    var positional = new List<string>();

    for (var i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        switch (arg)
        {
            case "-f":
            case "--format":
                flags.Format = ParseFormat(NextArg(args, ref i, arg));
                break;
            case "-o":
            case "--output":
                flags.OutputPath = NextArg(args, ref i, arg);
                break;
            case "--commit":
                flags.Commit = NextArg(args, ref i, arg);
                break;
            case "--compact":
                flags.Compact = true;
                break;
            case "-v":
            case "--verbose":
                flags.Verbose = true;
                break;
            case "--show-edges":
                flags.ShowEdges = true;
                break;
            case "--max-nodes":
                flags.MaxNodes = int.Parse(NextArg(args, ref i, arg));
                break;
            case "--max-edges":
                flags.MaxEdges = int.Parse(NextArg(args, ref i, arg));
                break;
            case "--fail-on-risk":
                flags.FailOnRisk = true;
                break;
            case "--no-stats":
                flags.NoStats = true;
                break;
            case "--no-diagnostics":
                flags.NoDiagnostics = true;
                break;
            case "--no-risk":
                flags.NoRisk = true;
                break;
            case "--no-risks":
                flags.NoRisks = true;
                break;
            case "--no-nodes":
                flags.NoNodes = true;
                break;
            default:
                if (arg.StartsWith('-'))
                {
                    throw new InvalidOperationException($"Unknown option: {arg}");
                }
                positional.Add(arg);
                break;
        }
    }

    return (flags, positional.ToArray());
}

static string NextArg(string[] args, ref int i, string flag)
{
    if (i + 1 >= args.Length)
    {
        throw new InvalidOperationException($"Missing value for {flag}");
    }
    i++;
    return args[i];
}

static DiffOutputFormat ParseFormat(string value) => value.ToLowerInvariant() switch
{
    "text" => DiffOutputFormat.Text,
    "md" or "markdown" => DiffOutputFormat.Markdown,
    "json" => DiffOutputFormat.Json,
    "html" => DiffOutputFormat.Html,
    "agent" => DiffOutputFormat.Agent,
    _ => throw new InvalidOperationException($"Unknown format: {value}")
};

static void PrintHelp(string? command)
{
  if (command is "compare")
  {
    Console.WriteLine("hoptracer compare <oldFile> <newFile> [options]");
    return;
  }
  if (command is "git")
  {
    Console.WriteLine("hoptracer git <file> [--commit <hash>] [options]");
    return;
  }

  Console.WriteLine("""
hoptracer - Compare Grasshopper definition files (.gh / .ghx)

Usage:
  hoptracer compare <oldFile> <newFile> [options]
  hoptracer git <file> [--commit <hash>] [options]
  hoptracer help [command]

Options:
  -f, --format <text|md|json|html|agent>   Output format (default: text)
  -o, --output <file>                Write to file instead of stdout
  --commit <hash>                    Git commit to compare against (git command)
  --compact                          Compact output
  -v, --verbose                      Include port/connection details
  --show-edges                       Include changed edges
  --max-nodes <n>                    Max changed nodes shown (default: 20)
  --max-edges <n>                    Max changed edges shown (default: 10)
  --fail-on-risk                     Exit 1 on critical/high risks
  --no-stats                         Omit statistics
  --no-diagnostics                   Omit diagnostics
  --no-risk                          Omit risk summary
  --no-risks                         Omit top risks list
  --no-nodes                         Omit changed nodes list
""");
}

sealed class CliFlags
{
    public DiffOutputFormat Format { get; set; } = DiffOutputFormat.Text;
    public string? OutputPath { get; set; }
    public string? Commit { get; set; }
    public string? FileOldLabel { get; set; }
    public string? FileNewLabel { get; set; }
    public bool Compact { get; set; }
    public bool Verbose { get; set; }
    public bool ShowEdges { get; set; }
    public int? MaxNodes { get; set; }
    public int? MaxEdges { get; set; }
    public bool FailOnRisk { get; set; }
    public bool NoStats { get; set; }
    public bool NoDiagnostics { get; set; }
    public bool NoRisk { get; set; }
    public bool NoRisks { get; set; }
    public bool NoNodes { get; set; }

    public DiffOutputOptions ToOptions(string fileOld, string fileNew)
    {
        var isAgent = Format == DiffOutputFormat.Agent;
        return new DiffOutputOptions
        {
            FileOld = FileOldLabel ?? fileOld,
            FileNew = FileNewLabel ?? fileNew,
            CompactFormat = Compact,
            IncludeNodeDetails = Verbose && !isAgent,
            IncludeChangedEdges = ShowEdges || isAgent,
            MaxNodesToShow = MaxNodes ?? (isAgent ? 100 : 20),
            MaxEdgesToShow = MaxEdges ?? (isAgent ? 50 : 10),
            IncludeStatistics = !NoStats && !isAgent,
            IncludeDiagnostics = !NoDiagnostics,
            IncludeRiskSummary = !NoRisk,
            IncludeTopRisks = !NoRisks,
            IncludeChangedNodes = !NoNodes && !isAgent
        };
    }
}
