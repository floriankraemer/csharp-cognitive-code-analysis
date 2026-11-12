/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysisConsoleApp.Tests.Reports;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Application;

/// <summary>
/// Drives the real composition root (<see cref="Program.Main"/>) in-process, once with a plain and once with an
/// interactive terminal, so the user-visible behaviour is verified end to end and measured by coverage.
/// </summary>
public class ProgramEndToEndTests
{
    private string _dir = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"cca-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(
            Path.Combine(_dir, "Sample.cs"),
            """
            namespace Samples;

            public class Helper { public int Twice(int x) => x * 2; }

            public class Sample
            {
                private readonly Helper _helper = new();
                public int Count { get; set; }

                public int Run(int a, int b)
                {
                    if (a > 0)
                    {
                        if (b > 0) { return _helper.Twice(a) + Count; }
                    }
                    else
                    {
                        return -1;
                    }

                    foreach (var i in new[] { a, b }) { Count += i; }
                    return 0;
                }
            }
            """);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    private string Path_(string name) => Path.Combine(_dir, name);

    private (int ExitCode, ConsoleOutputCapture Output) Run(bool interactive, params string[] args)
    {
        var capture = new ConsoleOutputCapture(interactive: interactive);
        int exitCode = Program.Main(args);
        return (exitCode, capture);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ConsoleReport_PrintsClassTableAndSummary(bool interactive)
    {
        var (exitCode, output) = Run(interactive, _dir, "--show-halstead", "--show-cyclomatic", "--show-coupling");
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text, Does.Contain("Summary:"));
            Assert.That(output.Text, Does.Contain("Total Methods Processed: 2"));
            Assert.That(output.Text, Does.Contain("Class: Samples.Sample"));
            Assert.That(output.Text, Does.Contain("Coupling: In=0, Out=1"));
            Assert.That(output.Text + output.StdOut, Does.Contain("Config:"));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void HtmlReport_IsWrittenAndAnnounced(bool interactive)
    {
        string report = Path_("out/report.html");

        var (exitCode, output) = Run(interactive, _dir, "-f", "Html", "-o", report);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(File.ReadAllText(report), Does.Contain("Samples.Sample"));
            Assert.That(output.Text + output.StdOut, Does.Contain("Html report generated: " + report));
        }
    }

    [Test]
    public void BaselineRoundTrip_JsonSnapshotThenComparedConsoleReport()
    {
        string baseline = Path_("baseline.json");
        var (first, o1) = Run(false, _dir, "-f", "Json", "-o", baseline);
        o1.Dispose();
        Assert.That(first, Is.EqualTo(0));

        File.WriteAllText(
            Path_("Sample.cs"),
            File.ReadAllText(Path_("Sample.cs")).Replace("return 0;", "if (a == b) { return 1; } return 0;"));

        var (second, o2) = Run(false, _dir, "-b", baseline, "-f", "Csv", "-o", Path_("r.csv"));
        using (o2)
        {
            Assert.That(second, Is.EqualTo(0));
            Assert.That(File.ReadAllText(Path_("r.csv")), Does.Contain("TotalScoreDelta"));
        }

        var (third, o3) = Run(true, _dir, "-b", baseline, "--show-coupling");
        using (o3)
        {
            Assert.That(third, Is.EqualTo(0));
            Assert.That(o3.Text, Does.Contain("▲"));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EmptyDirectory_ReportsNoSourceFilesAndFails(bool interactive)
    {
        string empty = Path_("empty");
        Directory.CreateDirectory(empty);

        var (exitCode, output) = Run(interactive, empty);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(-1));
            Assert.That(output.Text + output.StdOut, Does.Contain("No C# files found in " + empty));
        }
    }

    [TestCase("Html")]
    [TestCase("ConsoleText")]
    public void UnreadableCoverageFile_WarnsAndFails(string format)
    {
        string coverage = Path_("coverage.xml");
        File.WriteAllText(coverage, "<unknown/>");

        var (exitCode, output) = Run(false, _dir, "--coverage-cobertura", coverage, "-f", format, "-o", Path_("x.html"));
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(-1));
            Assert.That(output.Text + output.StdOut, Does.Contain("Warning: Failed to load coverage data"));
        }
    }

    [Test]
    public void CoberturaCoverage_IsAppliedToTheReport()
    {
        string coverage = Path_("coverage.xml");
        File.WriteAllText(coverage, $"""
            <coverage><sources><source>{_dir}</source></sources><packages><package name="Samples"><classes>
              <class name="Sample" filename="Sample.cs" lines-covered="3" lines-valid="4" branches-covered="1" branches-valid="2">
                <methods/>
              </class>
            </classes></package></packages></coverage>
            """);

        var (exitCode, output) = Run(false, _dir, "--coverage-cobertura", coverage);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text, Does.Contain("Line: 75.0%"));
            Assert.That(output.Text, Does.Contain("Branch: 50.0%"));
        }
    }

    [Test]
    public void Verbose_WritesTimestampedTraceToStandardError()
    {
        var (exitCode, output) = Run(false, _dir, "-v", "-f", "Markdown", "-o", Path_("r.md"));
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.StdErr, Does.Contain("Run started (report=Markdown"));
            Assert.That(output.StdErr, Does.Contain("Finding source files..."));
            Assert.That(output.StdErr, Does.Contain("Writing Markdown report done"));
        }
    }

    [Test]
    public void Verbose_ConsoleReport_TracesPostProcessing()
    {
        var (exitCode, output) = Run(false, _dir, "-v");
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.StdErr, Does.Contain("Run started (report=ConsoleText"));
        }
    }

    [Test]
    public void GenerateConfig_WritesFileAndAnnouncesIt()
    {
        var (exitCode, output) = Run(false, "--generate-config", _dir);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(File.Exists(Path_("cognitive-metrics-settings.json")), Is.True);
            Assert.That(output.Text + output.StdOut, Does.Contain("Config file created:"));
        }
    }

    [Test]
    public void GenerateConfig_WithoutPath_UsesCurrentDirectory()
    {
        string previous = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_dir);
        try
        {
            var (exitCode, output) = Run(false, "--generate-config");
            using (output)
            {
                Assert.That(exitCode, Is.EqualTo(0));
                Assert.That(File.Exists(Path_("cognitive-metrics-settings.json")), Is.True);
            }
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }

    [Test]
    public void MissingConfigFile_IsReportedAsError()
    {
        var (exitCode, output) = Run(false, _dir, "-c", Path_("nope.json"));
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(-1));
            Assert.That(output.Text + output.StdOut, Does.Contain("Error:"));
        }
    }
}
