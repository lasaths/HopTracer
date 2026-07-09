using HopTracer.Core.Models;
using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;
using System.Text.Json;

namespace GhDiffTool;

public static class CliRunner
{
    public static int Run(string[] args, TextWriter? stdout = null, TextWriter? stderr = null)
    {
        stdout ??= Console.Out;
        stderr ??= Console.Error;

        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            PrintHelp(stdout, args.Length > 1 ? args[1] : null);
            return 0;
        }

        if (args[0] is "--version" or "-V")
        {
            stdout.WriteLine(GetVersion());
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        CliFlags flags;
        string[] positional;
        try
        {
            (flags, positional) = ParseArgs(args.Skip(1).ToArray());
        }
        catch (Exception ex)
        {
            return Fail(stderr, InferFormatFromArgs(args), "error", GetDeepMessage(ex), $"Error: {GetDeepMessage(ex)}");
        }

        try
        {
            return command switch
            {
                "compare" => RunCompare(positional, flags, stdout, stderr),
                "git" => RunGit(positional, flags, stdout, stderr),
                _ => Fail(stderr, flags.Format, "unknown_command", $"Unknown command: {command}", $"Unknown command: {command}")
            };
        }
        catch (Exception ex)
        {
            return Fail(stderr, flags.Format, "error", GetDeepMessage(ex), $"Error: {GetDeepMessage(ex)}");
        }
    }

    public static string GetVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "hoptracer unknown" : $"hoptracer {version.Major}.{version.Minor}.{version.Build}";
    }

    public static (CliFlags Flags, string[] Positional) ParseArgsForTests(string[] args) => ParseArgs(args);

    private static string GetDeepMessage(Exception ex)
    {
        while (ex.InnerException != null) ex = ex.InnerException;
        return ex.Message;
    }

    private static int RunCompare(string[] positional, CliFlags flags, TextWriter stdout, TextWriter stderr)
    {
        if (positional.Length < 2)
        {
            return Fail(stderr, flags.Format, "usage_error", "compare requires old and new file paths",
                "Usage: hoptracer compare <oldFile> <newFile> [options]");
        }

        var oldPath = Path.GetFullPath(positional[0]);
        var newPath = Path.GetFullPath(positional[1]);
        if (!File.Exists(oldPath))
        {
            return Fail(stderr, flags.Format, "file_not_found", $"File not found: {oldPath}", $"File not found: {oldPath}", oldPath);
        }
        if (!File.Exists(newPath))
        {
            return Fail(stderr, flags.Format, "file_not_found", $"File not found: {newPath}", $"File not found: {newPath}", newPath);
        }

        var diff = ComputeDiff(oldPath, newPath);
        return WriteOutput(diff, oldPath, newPath, flags, stdout, stderr);
    }

    private static int RunGit(string[] positional, CliFlags flags, TextWriter stdout, TextWriter stderr)
    {
        if (positional.Length < 1)
        {
            return Fail(stderr, flags.Format, "usage_error", "git requires a file path",
                "Usage: hoptracer git <file> [--commit <hash>] [options]");
        }

        var filePath = Path.GetFullPath(positional[0]);
        if (!File.Exists(filePath))
        {
            return Fail(stderr, flags.Format, "file_not_found", $"File not found: {filePath}", $"File not found: {filePath}", filePath);
        }

        var dir = Path.GetDirectoryName(filePath) ?? ".";
        var git = new GitWrapper(dir, NullLogger<GitWrapper>.Instance);
        if (!git.IsGitRepo())
        {
            return Fail(stderr, flags.Format, "not_git_repository", $"Not a git repository: {dir}", $"Not a git repository: {dir}");
        }

        var commitHash = flags.Commit;
        if (string.IsNullOrWhiteSpace(commitHash))
        {
            var commits = git.GetCommits(filePath, 1);
            if (commits.Count == 0)
            {
                return Fail(stderr, flags.Format, "no_git_history", $"No git history found for: {filePath}", $"No git history found for: {filePath}", filePath);
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
            return WriteOutput(diff, flags.FileOldLabel, flags.FileNewLabel, flags, stdout, stderr);
        }
        finally
        {
            TryDelete(oldTemp);
        }
    }

    private static DiffComputation ComputeDiff(string oldPath, string newPath)
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

    private static string PreparePath(string path, ConverterService converter, List<string> temps)
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

    private static int WriteOutput(DiffComputation diff, string fileOld, string fileNew, CliFlags flags, TextWriter stdout, TextWriter stderr)
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
            stdout.Write(text);
        }

        if (flags.FailOnRisk && diff.RiskSummary.CriticalCount + diff.RiskSummary.HighCount > 0)
        {
            return 1;
        }

        return 0;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* ponytail: best-effort temp cleanup */ }
    }

    private static DiffOutputFormat InferFormatFromArgs(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] is not ("-f" or "--format"))
            {
                continue;
            }

            return args[i + 1].ToLowerInvariant() switch
            {
                "json" => DiffOutputFormat.Json,
                "agent" => DiffOutputFormat.Agent,
                _ => DiffOutputFormat.Text
            };
        }

        return DiffOutputFormat.Text;
    }

    private static int Fail(TextWriter stderr, DiffOutputFormat format, string code, string detail, string message, string? path = null)
    {
        if (UsesStructuredErrors(format))
        {
            var payload = new Dictionary<string, object?>
            {
                ["error"] = code,
                ["message"] = message
            };
            if (!string.IsNullOrWhiteSpace(path))
            {
                payload["path"] = path;
            }
            else if (!string.Equals(detail, message, StringComparison.Ordinal))
            {
                payload["detail"] = detail;
            }

            stderr.WriteLine(JsonSerializer.Serialize(payload));
        }
        else
        {
            stderr.WriteLine(message);
        }

        return 1;
    }

    private static bool UsesStructuredErrors(DiffOutputFormat format) =>
        format is DiffOutputFormat.Agent or DiffOutputFormat.Json;

    private static (CliFlags Flags, string[] Positional) ParseArgs(string[] args)
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

    private static string NextArg(string[] args, ref int i, string flag)
    {
        if (i + 1 >= args.Length)
        {
            throw new InvalidOperationException($"Missing value for {flag}");
        }
        i++;
        return args[i];
    }

    private static DiffOutputFormat ParseFormat(string value) => value.ToLowerInvariant() switch
    {
        "text" => DiffOutputFormat.Text,
        "md" or "markdown" => DiffOutputFormat.Markdown,
        "json" => DiffOutputFormat.Json,
        "html" => DiffOutputFormat.Html,
        "agent" => DiffOutputFormat.Agent,
        _ => throw new InvalidOperationException($"Unknown format: {value}")
    };

    private static void PrintHelp(TextWriter stdout, string? command)
    {
        if (command is "compare")
        {
            stdout.WriteLine("hoptracer compare <oldFile> <newFile> [options]");
            stdout.WriteLine();
            PrintOptions(stdout);
            return;
        }
        if (command is "git")
        {
            stdout.WriteLine("hoptracer git <file> [--commit <hash>] [options]");
            stdout.WriteLine();
            PrintOptions(stdout);
            return;
        }

        stdout.WriteLine($"""
{GetVersion()} - Compare Grasshopper definition files (.gh / .ghx)

Usage:
  hoptracer compare <oldFile> <newFile> [options]
  hoptracer git <file> [--commit <hash>] [options]
  hoptracer help [command]
  hoptracer --version
""");
        PrintOptions(stdout);
    }

    private static void PrintOptions(TextWriter stdout)
    {
        stdout.WriteLine("""
Options:
  -f, --format <text|md|json|html|agent>   Output format (default: text)
  -o, --output <file>                Write to file instead of stdout
  --commit <hash>                    Git commit to compare against (git command)
  --compact                          Compact output
  -v, --verbose                      Include port/connection details
  --show-edges                       Include changed edges
  --max-nodes <n>                    Max changed nodes shown (default: 20, agent: 100)
  --max-edges <n>                    Max changed edges shown (default: 10, agent: 50)
  --fail-on-risk                     Exit 1 on critical/high risks
  --no-stats                         Omit statistics
  --no-diagnostics                   Omit diagnostics
  --no-risk                          Omit risk summary
  --no-risks                         Omit top risks list
  --no-nodes                         Omit changed nodes list
  --version, -V                      Show version
""");
    }
}
