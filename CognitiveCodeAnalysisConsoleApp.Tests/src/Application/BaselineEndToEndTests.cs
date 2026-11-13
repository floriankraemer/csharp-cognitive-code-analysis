/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.Json;

using CognitiveCodeAnalysisConsoleApp.Tests.Reports;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Application;

/// <summary>
/// Generating, auto-detecting and applying baselines through <see cref="Program.Main"/>, with the working directory
/// set to the project, as a user runs it.
/// </summary>
public class BaselineEndToEndTests
{
    private const string Sample = """
        namespace Demo;

        public class Worker
        {
            public int Run(int a, int b)
            {
                return a + b;
            }
        }
        """;

    private const string Busier = """
        namespace Demo;

        public class Worker
        {
            public int Run(int a, int b)
            {
                int sum = 0;
                for (int i = 0; i < a; i++)
                {
                    if (i > b)
                    {
                        if (i % 2 == 0) { sum += i; } else { sum -= i; }
                    }
                }

                return sum + a + b;
            }
        }
        """;

    private string _root = null!;
    private string _originalDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _root = Path.Combine(Path.GetTempPath(), $"cca-baseline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_root, "project", "src"));
        File.WriteAllText(Path.Combine(_root, "project", "src", "Worker.cs"), Sample);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_root, recursive: true);
    }

    private string Report => Path.Combine(_root, "r.json");

    private string Project => Path.Combine(_root, "project");

    private string BaselineDirectory => Path.Combine(Project, ".cca", "baseline");

    private (int ExitCode, string Output) Run(string workingDirectory, params string[] args)
    {
        Directory.SetCurrentDirectory(workingDirectory);
        using var capture = new ConsoleOutputCapture(interactive: false);
        int exitCode = Program.Main(args);
        return (exitCode, capture.Text + capture.StdOut);
    }

    private string[] Baselines() =>
        Directory.Exists(BaselineDirectory) ? Directory.GetFiles(BaselineDirectory).Order().ToArray() : [];

    /// <summary>The delta of the total score of the only method in a Json report, null when it has no baseline.</summary>
    private static double? TotalScoreDelta(string reportPath)
    {
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(reportPath));
        JsonElement method = json.RootElement.GetProperty("methods")[0];
        return method.TryGetProperty("deltas", out JsonElement deltas) && deltas.TryGetProperty("totalScore", out JsonElement score)
            ? score.GetDouble()
            : null;
    }

    [Test]
    public void Generate_WritesARelativePathBaseline_AndAnnouncesIt()
    {
        var (exitCode, output) = Run(Project, "src", "-g");

        string file = Baselines().Single();
        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(file));
        var method = json.RootElement.GetProperty("methods")[0];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(
                output,
                Does.Contain("Baseline file generated: " + Path.Combine(".cca", "baseline", Path.GetFileName(file))));
            Assert.That(Path.GetFileName(file), Does.Match(@"^baseline-\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}\.json$"));
            Assert.That(json.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(1));
            Assert.That(json.RootElement.GetProperty("configHash").GetString(), Does.Match("^[0-9a-f]{64}$"));
            Assert.That(method.GetProperty("filePath").GetString(), Is.EqualTo("src/Worker.cs"));
            Assert.That(output, Does.Not.Contain("Auto-detected"));
        }
    }

    [Test]
    public void LongFormOfTheFlag_Works()
    {
        var (exitCode, _) = Run(Project, "src", "--generate-baseline");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Baselines(), Has.Length.EqualTo(1));
    }

    [Test]
    public void WithoutTheFlag_NothingIsWritten()
    {
        var (exitCode, output) = Run(Project, "src");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Directory.Exists(Path.Combine(Project, ".cca")), Is.False);
        Assert.That(output, Does.Not.Contain("Baseline file generated"));
    }

    [Test]
    public void GenerateThenModifyThenRun_AutoDetectsTheBaselineAndShowsDeltas()
    {
        Run(Project, "src", "-g");
        string name = Path.GetFileName(Baselines().Single());
        File.WriteAllText(Path.Combine(Project, "src", "Worker.cs"), Busier);

        var (exitCode, output) = Run(Project, "src", "-f", "Json", "-o", Report);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output, Does.Contain("Auto-detected latest baseline file: " + name));
            Assert.That(output, Does.Not.Contain("Warning"));
            Assert.That(TotalScoreDelta(Report), Is.GreaterThan(0));
        }
    }

    [Test]
    public void AutoDetect_AlsoWorksForTheConsoleReport()
    {
        Run(Project, "src", "-g");
        File.WriteAllText(Path.Combine(Project, "src", "Worker.cs"), Busier);

        var (exitCode, output) = Run(Project, "src");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Contain("Auto-detected latest baseline file: baseline-"));
    }

    [Test]
    public void WithoutTheBaselineDirectory_NothingIsAutoDetected()
    {
        var (exitCode, output) = Run(Project, "src", "-f", "Json", "-o", Report);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Not.Contain("Auto-detected"));
        Assert.That(TotalScoreDelta(Report), Is.Null);
    }

    [Test]
    public void AnExplicitBaseline_WinsOverTheAutoDetectedOne()
    {
        Run(Project, "src", "-g");
        string explicitBaseline = Path.Combine(_root, "explicit.json");
        Run(Project, "src", "-f", "Json", "-o", explicitBaseline);

        var (exitCode, output) = Run(Project, "src", "-b", explicitBaseline, "-f", "Json", "-o", Report);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Not.Contain("Auto-detected"));
    }

    [Test]
    public void Generate_AppliesTheExistingBaselineFirst_AndNeverOverwritesIt()
    {
        Run(Project, "src", "-g");
        string first = Baselines().Single();
        string firstContent = File.ReadAllText(first);
        File.WriteAllText(Path.Combine(Project, "src", "Worker.cs"), Busier);

        var (exitCode, output) = Run(Project, "src", "-g", "-f", "Json", "-o", Report);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Contain("Auto-detected latest baseline file:"));
        Assert.That(output, Does.Contain("Baseline file generated:"));
        Assert.That(TotalScoreDelta(Report), Is.GreaterThan(0));
        Assert.That(Baselines(), Has.Length.EqualTo(2));
        Assert.That(File.ReadAllText(first), Is.EqualTo(firstContent));
    }

    [Test]
    public void AutoDetect_ChoosesTheNewestFile()
    {
        Directory.CreateDirectory(BaselineDirectory);
        File.WriteAllText(Path.Combine(BaselineDirectory, "baseline-2020-01-01_00-00-00.json"), "{}");
        File.WriteAllText(Path.Combine(BaselineDirectory, "baseline-2030-01-01_00-00-00.json"), "{}");
        File.WriteAllText(Path.Combine(BaselineDirectory, "baseline-2040-13-01_00-00-00.json"), "not json");

        var (exitCode, output) = Run(Project, "src");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Contain("Auto-detected latest baseline file: baseline-2030-01-01_00-00-00.json"));
    }

    [Test]
    public void AnInvalidBaseline_FailsWithAllErrors()
    {
        string baseline = Path.Combine(_root, "bad.json");
        File.WriteAllText(baseline, """{"methods": [{"totalScore": -1, "filePath": 3}]}""");

        var (exitCode, output) = Run(Project, "src", "-b", baseline);

        Assert.That(exitCode, Is.EqualTo(-1));
        Assert.That(
            output,
            Does.Contain($"Invalid baseline file format in {baseline}: methods[0].totalScore must be a finite, non-negative number, "
                + "methods[0].filePath must be a string"));
    }

    [Test]
    public void ChangedMetricSettings_WarnAboutTheConfigHash()
    {
        Run(Project, "src", "-g");
        File.WriteAllText(
            Path.Combine(Project, "settings.json"),
            """{ "cognitive": { "metrics": { "ifCount": { "threshold": 9, "scale": 3.0, "enabled": true } } } }""");

        var (exitCode, output) = Run(Project, "src", "-c", "settings.json");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(
            output,
            Does.Match(@"Warning: Baseline config hash \([0-9a-f]{64}\) does not match current config hash \([0-9a-f]{64}\)\. Metrics comparison may not be accurate\."));
    }

    [Test]
    public void BaselineWithoutAHash_NeverWarns()
    {
        string baseline = Path.Combine(_root, "old.json");
        Run(Project, "src", "-f", "Json", "-o", baseline);
        File.WriteAllText(
            Path.Combine(Project, "settings.json"),
            """{ "cognitive": { "metrics": { "ifCount": { "threshold": 9, "scale": 3.0, "enabled": true } } } }""");

        var (exitCode, output) = Run(Project, "src", "-c", "settings.json", "-b", baseline);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Not.Contain("config hash"));
    }

    [Test]
    public void OldAbsolutePathBaseline_StillMatchesAfterTheProjectMoved()
    {
        // A baseline of the Json report keeps absolute paths, as every baseline did before relative paths.
        string baseline = Path.Combine(_root, "old.json");
        Run(Project, "src", "-f", "Json", "-o", baseline);
        Assert.That(File.ReadAllText(baseline), Does.Contain(Path.Combine(Project, "src", "Worker.cs").Replace("\\", "\\\\")));

        string moved = Path.Combine(_root, "moved");
        Directory.Move(Project, moved);
        File.WriteAllText(Path.Combine(moved, "src", "Worker.cs"), Busier);

        var (exitCode, _) = Run(moved, "src", "-b", baseline, "-f", "Json", "-o", Report);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(TotalScoreDelta(Report), Is.GreaterThan(0));
    }

    [Test]
    public void RelativeBaseline_MatchesInAnotherCheckoutPath()
    {
        Run(Project, "src", "-g");
        string baseline = Baselines().Single();
        string copy = Path.Combine(_root, "copy");
        Directory.CreateDirectory(Path.Combine(copy, "src"));
        File.WriteAllText(Path.Combine(copy, "src", "Worker.cs"), Busier);

        var (exitCode, _) = Run(copy, "src", "-b", baseline, "-f", "Json", "-o", Report);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(TotalScoreDelta(Report), Is.GreaterThan(0));
    }
}
