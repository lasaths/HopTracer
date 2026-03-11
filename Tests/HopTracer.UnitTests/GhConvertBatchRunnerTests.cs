using HopTracer.Core.Services;
using Xunit;

namespace HopTracer.UnitTests;

public class GhConvertBatchRunnerTests : IDisposable
{
    private readonly string _tempRoot;

    public GhConvertBatchRunnerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"hoptracer_convert_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public void TryParseConvertArguments_ReturnsFalse_WhenNotConvertMode()
    {
        var ok = GhConvertBatchRunner.TryParseConvertArguments(new[] { "--git", "file.gh" }, out var paths);

        Assert.False(ok);
        Assert.Empty(paths);
    }

    [Fact]
    public void TryParseConvertArguments_ParsesSingleAndMultiPathInputs()
    {
        var singleOk = GhConvertBatchRunner.TryParseConvertArguments(new[] { "--convert", "a.gh" }, out var singlePaths);
        var multiOk = GhConvertBatchRunner.TryParseConvertArguments(new[] { "--convert", "a.gh", "b.gh", " c.gh " }, out var multiPaths);

        Assert.True(singleOk);
        Assert.Single(singlePaths);
        Assert.Equal("a.gh", singlePaths[0]);

        Assert.True(multiOk);
        Assert.Equal(3, multiPaths.Count);
        Assert.Equal("a.gh", multiPaths[0]);
        Assert.Equal("b.gh", multiPaths[1]);
        Assert.Equal("c.gh", multiPaths[2]);
    }

    [Fact]
    public void Run_ReturnsFailures_ForDependencyErrors()
    {
        var converter = new FakeConverterService
        {
            DependenciesAvailable = false,
            DependencyMessage = "GH_IO dependency unavailable."
        };
        var runner = new GhConvertBatchRunner(converter);

        var summary = runner.Run(
            new[] { "C:\\temp\\one.gh", "C:\\temp\\two.gh" },
            (_, _) => GhConvertOverwriteDecision.Overwrite);

        Assert.Equal(2, summary.RequestedCount);
        Assert.Equal(0, summary.ConvertedCount);
        Assert.Equal(0, summary.SkippedCount);
        Assert.Equal(2, summary.FailedCount);
        Assert.False(summary.WasCancelled);
        Assert.All(summary.Results, r => Assert.Equal(GhConvertFileStatus.Failed, r.Status));
    }

    [Fact]
    public void Run_RespectsOverwriteSkipAndCancelDecisions()
    {
        var first = CreateTempFile("first.gh");
        var second = CreateTempFile("second.gh");
        var third = CreateTempFile("third.gh");
        var fourth = CreateTempFile("fourth.gh");

        File.WriteAllText(Path.ChangeExtension(first, ".ghx"), "existing");
        File.WriteAllText(Path.ChangeExtension(second, ".ghx"), "existing");
        File.WriteAllText(Path.ChangeExtension(third, ".ghx"), "existing");

        var converter = new FakeConverterService();
        var runner = new GhConvertBatchRunner(converter);

        var decisionQueue = new Queue<GhConvertOverwriteDecision>(new[]
        {
            GhConvertOverwriteDecision.Skip,
            GhConvertOverwriteDecision.Overwrite,
            GhConvertOverwriteDecision.Cancel
        });

        var summary = runner.Run(
            new[] { first, second, third, fourth },
            (_, _) => decisionQueue.Dequeue());

        Assert.Equal(4, summary.RequestedCount);
        Assert.Equal(1, summary.ConvertedCount);
        Assert.Equal(1, summary.SkippedCount);
        Assert.Equal(0, summary.FailedCount);
        Assert.True(summary.WasCancelled);
        Assert.Equal(3, summary.Results.Count);
        Assert.Equal(1, converter.ConvertCalls);

        Assert.Contains(summary.Results, r => r.Status == GhConvertFileStatus.Cancelled);
        Assert.DoesNotContain(summary.Results, r => string.Equals(r.InputPath, fourth, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Run_HandlesMixedInputValidationAndConversionErrors()
    {
        var good = CreateTempFile("good.gh");
        var badExt = CreateTempFile("bad.txt");
        var missing = Path.Combine(_tempRoot, "missing.gh");
        var throws = CreateTempFile("throws.gh");

        var converter = new FakeConverterService();
        converter.ThrowOn.Add(throws);

        var runner = new GhConvertBatchRunner(converter);
        var summary = runner.Run(
            new[] { good, badExt, missing, throws },
            (_, _) => GhConvertOverwriteDecision.Overwrite);

        Assert.Equal(4, summary.RequestedCount);
        Assert.Equal(1, summary.ConvertedCount);
        Assert.Equal(1, summary.SkippedCount);
        Assert.Equal(2, summary.FailedCount);
        Assert.False(summary.WasCancelled);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }

    private string CreateTempFile(string name)
    {
        var path = Path.Combine(_tempRoot, name);
        File.WriteAllText(path, "test");
        return path;
    }

    private sealed class FakeConverterService : IConverterService
    {
        public bool DependenciesAvailable { get; set; } = true;
        public string DependencyMessage { get; set; } = "GH_IO ready";
        public HashSet<string> ThrowOn { get; } = new(StringComparer.OrdinalIgnoreCase);
        public int ConvertCalls { get; private set; }

        public string ConvertGhToGhx(string inputPath)
        {
            ConvertCalls++;
            if (ThrowOn.Contains(inputPath))
            {
                throw new InvalidOperationException("Simulated conversion failure.");
            }

            var outputPath = Path.ChangeExtension(inputPath, ".ghx");
            File.WriteAllText(outputPath, "converted");
            return outputPath;
        }

        public bool TryCheckDependencies(out string message)
        {
            message = DependencyMessage;
            return DependenciesAvailable;
        }
    }
}
