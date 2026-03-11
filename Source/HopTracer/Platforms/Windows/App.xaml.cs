using HopTracer.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml;
using System.Text;
using System.Windows.Forms;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HopTracer.Maui.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
    private const string ConversionDialogTitle = "HopTracer GH to GHX Conversion";

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
        if (GhConvertBatchRunner.TryParseConvertArguments(args, out var convertPaths))
        {
            var exitCode = RunConvertMode(convertPaths);
            Environment.Exit(exitCode);
        }
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    private static int RunConvertMode(IReadOnlyList<string> inputPaths)
    {
        if (inputPaths.Count == 0)
        {
            MessageBox.Show(
                "No input files were provided.\n\nUsage:\nHopTracer.exe --convert <path1.gh> [path2.gh ...]",
                ConversionDialogTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        if (!DependencyHelper.TryEnsureGhIoDll(out var dependencyMessage))
        {
            var message = string.IsNullOrWhiteSpace(dependencyMessage)
                ? "GH_IO dependency unavailable."
                : dependencyMessage;
            MessageBox.Show(message, ConversionDialogTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }

        var runner = new GhConvertBatchRunner(new ConverterService(NullLogger<ConverterService>.Instance));
        var summary = runner.Run(inputPaths, PromptOverwriteDecision);

        ShowSummary(summary);

        return (summary.FailedCount > 0 || summary.WasCancelled) ? 1 : 0;
    }

    private static GhConvertOverwriteDecision PromptOverwriteDecision(string inputPath, string outputPath)
    {
        var result = MessageBox.Show(
            $"Output file already exists:\n{outputPath}\n\nChoose Yes to overwrite, No to skip this file, or Cancel to stop the batch.",
            ConversionDialogTitle,
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        return result switch
        {
            DialogResult.Yes => GhConvertOverwriteDecision.Overwrite,
            DialogResult.No => GhConvertOverwriteDecision.Skip,
            _ => GhConvertOverwriteDecision.Cancel
        };
    }

    private static void ShowSummary(GhConvertBatchSummary summary)
    {
        var lines = new List<string>
        {
            $"Requested: {summary.RequestedCount}",
            $"Converted: {summary.ConvertedCount}",
            $"Skipped: {summary.SkippedCount}",
            $"Failed: {summary.FailedCount}"
        };

        if (summary.WasCancelled)
        {
            lines.Add("Batch was cancelled before all files were processed.");
        }

        var issueLines = summary.Results
            .Where(static r => r.Status != GhConvertFileStatus.Converted)
            .Take(8)
            .Select(r =>
            {
                var shortName = string.IsNullOrWhiteSpace(r.InputPath)
                    ? "(unknown)"
                    : Path.GetFileName(r.InputPath);
                return $"- {shortName}: {r.Message}";
            })
            .ToArray();

        if (issueLines.Length > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Details:");
            lines.AddRange(issueLines);

            var hiddenCount = summary.Results.Count(static r => r.Status != GhConvertFileStatus.Converted) - issueLines.Length;
            if (hiddenCount > 0)
            {
                lines.Add($"- ... and {hiddenCount} more issue(s)." );
            }
        }

        var icon = summary.FailedCount > 0
            ? MessageBoxIcon.Error
            : (summary.WasCancelled || summary.SkippedCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);

        MessageBox.Show(string.Join(Environment.NewLine, lines), ConversionDialogTitle, MessageBoxButtons.OK, icon);
    }
}

