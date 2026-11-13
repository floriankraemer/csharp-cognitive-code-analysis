/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.Json;
using System.Text.RegularExpressions;

using CognitiveCodeAnalysisConsoleApp.Tests.Reports;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Commands;

/// <summary>
/// Drives the real composition root (<see cref="Program.Main"/>) with the <c>churn</c> command against a real git
/// repository, the way a user runs it.
/// </summary>
public class ChurnCommandEndToEndTests
{
    private const string Source = """
        namespace Samples;

        public class Helper { public int Twice(int x) => x * 2; }

        public class Sample
        {
            public int Run(int a, int b)
            {
                if (a > 0)
                {
                    if (b > 0) { return a + b; }
                }
                else
                {
                    return -1;
                }

                return 0;
            }
        }
        """;

    private TempGitRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = TempGitRepository.Create();
        _repo.WriteFile("Sample.cs", Source);
        _repo.Commit("Sample.cs", "2024-02-01T12:00:00+0000");
        _repo.Commit("Sample.cs", "2024-03-01T12:00:00+0000");
        _repo.Commit("Sample.cs", "2024-04-01T12:00:00+0000");
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    private static (int ExitCode, ConsoleOutputCapture Output) Run(params string[] args)
    {
        var capture = new ConsoleOutputCapture();
        int exitCode = Program.Main(args);
        return (exitCode, capture);
    }

    private static string AllOutput(ConsoleOutputCapture output) => output.Text + output.StdOut + output.StdErr;

    private string Out(string name) => Path.Combine(_repo.Directory, "out", name);

    private static (double Score, double Churn, int Changes) Row(string table, string className)
    {
        Match match = Regex.Match(table, $@"│ {Regex.Escape(className)}\s+│\s+(\S+)\s+│\s+(\S+)\s+│\s+(\d+)\s+│");
        Assert.That(match.Success, Is.True, $"no row for {className} in:\n{table}");
        return (double.Parse(match.Groups[1].Value), double.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value));
    }

    [Test]
    public void ConsoleTable_ShowsClassesWithScoreAndChurnAndHidesClassesWithoutScore()
    {
        var (exitCode, output) = Run("churn", _repo.Directory);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text, Does.Contain("│ Class").And.Contain("Score").And.Contain("Churn").And.Contain("Times Changed"));
            Assert.That(output.Text, Does.Not.Contain("Risk Churn"));
            (double score, double churn, int changes) = Row(output.Text, "Samples.Sample");
            Assert.That(changes, Is.EqualTo(3));
            Assert.That(score, Is.GreaterThan(0));
            Assert.That(churn, Is.EqualTo(Math.Round(score * 3, 3)).Within(0.0005));
            Assert.That(output.Text, Does.Not.Contain("Samples.Helper"));
        }
    }

    [Test]
    public void Since_LimitsTheCountedChanges()
    {
        var (exitCode, output) = Run("churn", _repo.Directory, "--since", "2024-03-01");
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(Row(output.Text, "Samples.Sample").Changes, Is.EqualTo(2));
        }

        var (_, later) = Run("churn", _repo.Directory, "-s", "2024-04-02");
        using (later)
        {
            Assert.That(later.Text, Does.Not.Contain("Samples.Sample"));
        }
    }

    [Test]
    public void PathDefaultsToTheCurrentDirectory()
    {
        string previous = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_repo.Directory);
        try
        {
            var (exitCode, output) = Run("churn");
            using (output)
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(Row(output.Text, "Samples.Sample").Changes, Is.EqualTo(3));
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }

    [TestCase("Json", "json")]
    [TestCase("json", "json")]
    [TestCase("CSV", "csv")]
    [TestCase("Html", "html")]
    [TestCase("markdown", "md")]
    [TestCase("SvgTreemap", "svg")]
    [TestCase("svg", "svg")]
    public void Report_IsWrittenToTheOutputFileAndAnnounced(string type, string extension)
    {
        string file = Out($"report.{extension}");

        var (exitCode, output) = Run("churn", _repo.Directory, "-r", type, "-o", file);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text + output.StdOut, Does.Contain("Churn report generated: " + file));
            string content = File.ReadAllText(file);
            Assert.That(content, Does.Contain("Samples.Sample"));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(file)!), Has.Length.EqualTo(1), "no temp file left behind");
        }
    }

    [Test]
    public void JsonReport_HasTheDocumentedShape()
    {
        string file = Out("report.json");
        var (exitCode, output) = Run("churn", _repo.Directory, "--report-type", "Json", "--output-file", file);
        output.Dispose();

        Assert.That(exitCode, Is.EqualTo(0));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
        JsonElement sample = document.RootElement.GetProperty("classes").GetProperty("Samples.Sample");
        Assert.That(document.RootElement.GetProperty("createdAt").GetString(), Does.Match(@"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}$"));
        Assert.That(sample.GetProperty("file").GetString(), Is.EqualTo(_repo.PathOf("Sample.cs")));
        Assert.That(sample.GetProperty("timesChanged").GetInt32(), Is.EqualTo(3));
        Assert.That(sample.GetProperty("churn").GetDouble(), Is.EqualTo(sample.GetProperty("score").GetDouble() * 3).Within(0.0005));
        Assert.That(sample.GetProperty("coverage").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(sample.GetProperty("riskLevel").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public void SvgReport_IsATreemapOfTheClassesWithChurn()
    {
        string file = Out("report.svg");
        var (_, output) = Run("churn", _repo.Directory, "-r", "SvgTreemap", "-o", file);
        output.Dispose();

        string svg = File.ReadAllText(file);
        Assert.That(svg, Does.StartWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<svg width=\"1200\" height=\"800\""));
        Assert.That(svg, Does.Contain("<title>Samples.Sample&#10;Churn: "));
        Assert.That(svg, Does.Not.Contain("Samples.Helper"));
    }

    private string WriteCobertura(int covered, int total)
    {
        string coverage = Path.Combine(_repo.Directory, "coverage.xml");
        File.WriteAllText(coverage, $"""
            <coverage><sources><source>{_repo.Directory}</source></sources><packages><package name="Samples"><classes>
              <class name="Sample" filename="Sample.cs" lines-covered="{covered}" lines-valid="{total}" branches-covered="0" branches-valid="0">
                <methods/>
              </class>
            </classes></package></packages></coverage>
            """);
        return coverage;
    }

    [Test]
    public void Coverage_AddsRiskChurnCoverageAndRiskLevelAndWarnsAboutClassesNotInTheReport()
    {
        string coverage = WriteCobertura(covered: 1, total: 4);

        var (exitCode, output) = Run("churn", _repo.Directory, "--coverage-cobertura", coverage);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text, Does.Contain("Risk Churn").And.Contain("Coverage").And.Contain("Risk Level"));
            Assert.That(output.Text, Does.Match(@"│ Samples\.Sample\s+│\s+\S+\s+│\s+\S+\s+│\s+\S+\s+│\s+3\s+│\s+25\.00%\s+│ (LOW|MEDIUM|HIGH|CRITICAL)"));
            Assert.That(AllOutput(output), Does.Contain("Warning: 1 of 2 classes are not in the coverage report"));
        }
    }

    [Test]
    public void Coverage_ReportsGetNotAvailableForClassesAbsentFromCoverage()
    {
        string coverage = WriteCobertura(covered: 4, total: 4);
        string file = Out("covered.json");

        var (exitCode, output) = Run("churn", _repo.Directory, "--coverage-cobertura", coverage, "-r", "Json", "-o", file);
        output.Dispose();

        Assert.That(exitCode, Is.EqualTo(0));
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
        JsonElement classes = document.RootElement.GetProperty("classes");
        Assert.That(classes.GetProperty("Samples.Sample").GetProperty("coverage").GetDouble(), Is.EqualTo(1));
        Assert.That(classes.GetProperty("Samples.Sample").GetProperty("riskChurn").GetDouble(), Is.Zero);
        Assert.That(classes.GetProperty("Samples.Sample").GetProperty("riskLevel").GetString(), Is.EqualTo("LOW"));
        Assert.That(classes.GetProperty("Samples.Helper").GetProperty("coverage").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(classes.GetProperty("Samples.Helper").GetProperty("riskLevel").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public void Coverage_AmbiguousShortNameIsNotAvailableAndCountedInTheWarning()
    {
        string coverage = Path.Combine(_repo.Directory, "ambiguous.xml");
        File.WriteAllText(coverage, """
            <coverage><packages>
              <package name="One"><classes><class name="Sample" filename="Sample.cs" lines-covered="1" lines-valid="2"><methods/></class></classes></package>
              <package name="Two"><classes><class name="Sample" filename="Other.cs" lines-covered="2" lines-valid="2"><methods/></class></classes></package>
            </packages></coverage>
            """);
        _repo.WriteFile("Sample.cs", Source.Replace("namespace Samples;", ""));

        var (exitCode, output) = Run("churn", _repo.Directory, "--coverage-cobertura", coverage);
        using (output)
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(AllOutput(output), Does.Contain("Warning: 2 of 2 classes are not in the coverage report"));
        }
    }

    [Test]
    public void Coverage_MissingFileFailsBeforeAnyAnalysis()
    {
        var (exitCode, output) = Run("churn", _repo.Directory, "--coverage-cobertura", Path.Combine(_repo.Directory, "nope.xml"));
        using (output)
        {
            Assert.That(exitCode, Is.Not.Zero);
            Assert.That(AllOutput(output), Does.Contain("Error: Coverage file not found: "));
        }
    }

    [Test]
    public void Coverage_UnreadableFileFailsWithAClearMessage()
    {
        string coverage = Path.Combine(_repo.Directory, "coverage.xml");
        File.WriteAllText(coverage, "<unknown/>");

        var (exitCode, output) = Run("churn", _repo.Directory, "--coverage-cobertura", coverage);
        using (output)
        {
            Assert.That(exitCode, Is.Not.Zero);
            Assert.That(AllOutput(output), Does.Contain("Error: Failed to load coverage data: Unrecognized coverage XML format"));
        }
    }

    [TestCase("--since", "yesterday", "Invalid --since date 'yesterday'. Expected format: yyyy-MM-dd.")]
    [TestCase("--vcs", "svn", "Unknown VCS 'svn'. Supported: git.")]
    [TestCase("-r", "Pdf", "Invalid report type 'Pdf'. Expected one of: Json, Csv, Html, Markdown, SvgTreemap.")]
    [TestCase("-r", "Json", "--report-type and --output-file must be given together.")]
    [TestCase("-o", "x.json", "--report-type and --output-file must be given together.")]
    public void InvalidOptions_PrintAMessageAndExitNonZeroWithoutAnalysing(string option, string value, string message)
    {
        var (exitCode, output) = Run("churn", _repo.Directory, option, value);
        using (output)
        {
            Assert.That(exitCode, Is.Not.Zero);
            Assert.That(AllOutput(output), Does.Contain(message));
            Assert.That(output.Text, Does.Not.Contain("Samples.Sample"));
        }
    }

    [Test]
    public void Since_InTheFutureIsRejectedButTodayIsAccepted()
    {
        string tomorrow = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var (exitCode, output) = Run("churn", _repo.Directory, "--since", tomorrow);
        using (output)
        {
            Assert.That(exitCode, Is.Not.Zero);
            Assert.That(AllOutput(output), Does.Contain("--since must not be in the future."));
            Assert.That(output.Text, Does.Not.Contain("Samples.Sample"));
        }

        string today = DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var (todayExit, todayOutput) = Run("churn", _repo.Directory, "--since", today);
        todayOutput.Dispose();
        Assert.That(todayExit, Is.Zero);
    }

    [Test]
    public void DirectoryWithoutSourceFiles_FailsWithAMessage()
    {
        string empty = Path.Combine(_repo.Directory, "empty");
        Directory.CreateDirectory(empty);

        var (exitCode, output) = Run("churn", empty);
        using (output)
        {
            Assert.That(exitCode, Is.Not.Zero);
            Assert.That(AllOutput(output), Does.Contain("No C# files found in " + empty));
        }
    }

    [Test]
    public void UnreadableConfigurationFile_FailsWithAMessage()
    {
        var (exitCode, output) = Run("churn", _repo.Directory, "-c", Path.Combine(_repo.Directory, "missing.json"));
        using (output)
        {
            Assert.That(exitCode, Is.Not.Zero);
            Assert.That(AllOutput(output), Does.Contain("Error: "));
        }
    }

    [Test]
    public void FilesOutsideAGitRepository_FailWithNotAGitRepository()
    {
        string plain = Path.Combine(Path.GetTempPath(), $"cca-churn-plain-{Guid.NewGuid():N}");
        Directory.CreateDirectory(plain);
        File.WriteAllText(Path.Combine(plain, "Sample.cs"), Source);
        try
        {
            var (exitCode, output) = Run("churn", plain);
            using (output)
            {
                Assert.That(exitCode, Is.Not.Zero);
                Assert.That(AllOutput(output), Does.Contain("Error: Not a git repository: " + plain));
            }
        }
        finally
        {
            Directory.Delete(plain, recursive: true);
        }
    }

    [Test]
    public void MissingGit_FailsWithAMessage()
    {
        string path = Environment.GetEnvironmentVariable("PATH")!;
        string empty = Path.Combine(_repo.Directory, "empty-path");
        Directory.CreateDirectory(empty);
        Environment.SetEnvironmentVariable("PATH", empty);
        try
        {
            var (exitCode, output) = Run("churn", _repo.Directory);
            using (output)
            {
                Assert.That(exitCode, Is.Not.Zero);
                Assert.That(AllOutput(output), Does.Contain("Error: git executable not found"));
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", path);
        }
    }

    [Test]
    public void ReportIsNotWrittenWhenGitFails()
    {
        string file = Out("never.json");
        string path = Environment.GetEnvironmentVariable("PATH")!;
        Environment.SetEnvironmentVariable("PATH", _repo.Directory);
        try
        {
            var (exitCode, output) = Run("churn", _repo.Directory, "-r", "Json", "-o", file);
            output.Dispose();

            Assert.That(exitCode, Is.Not.Zero);
            Assert.That(File.Exists(file), Is.False);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", path);
        }
    }

    [Test]
    public void Help_ListsTheChurnCommandAndItsOptions()
    {
        var (rootExit, root) = Run("--help");
        using (root)
        {
            Assert.That(rootExit, Is.Zero);
            Assert.That(root.Text, Does.Contain("churn"));
        }

        var (churnExit, churn) = Run("churn", "--help");
        using (churn)
        {
            Assert.That(churnExit, Is.Zero);
            Assert.That(churn.Text, Does.Contain("--since").And.Contain("--vcs").And.Contain("--report-type")
                .And.Contain("--output-file").And.Contain("--config").And.Contain("--coverage-cobertura"));
        }
    }

    [Test]
    public void AnalyseStaysTheDefaultCommand_PathFirstStillWorksAsBefore()
    {
        string report = Out("analyse.json");

        var (exitCode, output) = Run(_repo.Directory, "-f", "Json", "-o", report);
        using (output)
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(output.Text + output.StdOut, Does.Contain("Json report generated: " + report));
            Assert.That(File.ReadAllText(report), Does.Contain("Samples.Sample").And.Not.Contain("timesChanged"));
        }

        var (consoleExit, console) = Run(_repo.Directory);
        using (console)
        {
            Assert.That(consoleExit, Is.Zero);
            Assert.That(console.Text, Does.Contain("Class: Samples.Sample").And.Contain("Total Methods Processed"));
        }
    }

    [Test]
    public void AnalyseStaysTheDefaultCommand_ItsExistingOptionsAreUntouchedByChurn()
    {
        // `-f` is an analyse option; registering churn must not have changed it.
        var (exitCode, output) = Run(_repo.Directory, "-f", "Csv", "-o", Out("a.csv"), "--show-halstead");
        using (output)
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(File.ReadAllText(Out("a.csv")), Does.Contain("Samples.Sample"));
        }
    }
}
