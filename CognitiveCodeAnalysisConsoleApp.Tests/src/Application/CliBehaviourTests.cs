/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Text.RegularExpressions;

using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysisConsoleApp.Tests.Reports;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Application;

/// <summary>
/// End-to-end checks of the command line contract: strict options, case-insensitive report types, configuration
/// errors, honest console messages and culture-independent output.
/// </summary>
public class CliBehaviourTests
{
    private string _dir = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"cca-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(
            Path.Combine(_dir, "Sample.cs"),
            """
            namespace Samples;

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

                    foreach (var i in new[] { a, b }) { a += i; }
                    return 0;
                }
            }
            """);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    private string P(string name) => Path.Combine(_dir, name);

    private static (int ExitCode, ConsoleOutputCapture Output) Run(params string[] args)
    {
        var capture = new ConsoleOutputCapture();
        int exitCode = Program.Main(args);
        return (exitCode, capture);
    }

    private string WriteConfig(string cognitiveJson)
    {
        string path = P("config.json");
        File.WriteAllText(path, $$"""{ "cognitive": { {{cognitiveJson}} } }""");
        return path;
    }

    private string WriteCoverage()
    {
        string path = P("coverage.xml");
        File.WriteAllText(path, $"""
            <coverage><sources><source>{_dir}</source></sources><packages><package name="Samples"><classes>
              <class name="Sample" filename="Sample.cs" lines-covered="3" lines-valid="4" branches-covered="1" branches-valid="3">
                <methods/>
              </class>
            </classes></package></packages></coverage>
            """);
        return path;
    }

    // ---- unknown options ----

    [TestCase("--bogus")]
    [TestCase("--show-halstad")]
    [TestCase("-x")]
    public void UnknownOption_IsIgnoredWithAWarning_AndTheRunStillSucceeds(string option)
    {
        var (exitCode, output) = Run(_dir, option);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text + output.StdOut, Does.Contain($"Warning: Ignored unknown arguments: {option}"));
            Assert.That(output.Text, Does.Contain("Summary:"));
        }
    }

    [Test]
    public void UnknownOption_NextToValidOnes_IsListedAndTheValidOnesStillApply()
    {
        var (exitCode, output) = Run(_dir, "-f", "Html", "-o", P("r.html"), "--no-such-option", "value");
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(File.Exists(P("r.html")), Is.True);
            Assert.That(output.Text + output.StdOut, Does.Contain("Ignored unknown arguments: --no-such-option value"));
        }
    }

    [Test]
    public void SurplusPositionalArgument_KeepsItsExistingBehavior()
    {
        var (exitCode, output) = Run(_dir, P("other"));
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(-1));
        }
    }

    [Test]
    public void KnownOptions_StillWork()
    {
        var (exitCode, output) = Run(_dir, "--report-format", "Markdown", "--output-file", P("r.md"), "--show-halstead", "--show-cyclomatic", "--show-coupling", "-v");
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(File.Exists(P("r.md")), Is.True);
            Assert.That(output.Text + output.StdOut, Does.Not.Contain("Ignored unknown arguments"));
        }
    }

    [Test]
    public void ShowUnderstandability_FromTheCommandLineOrTheConfigFile_AppendsTheColumn()
    {
        const string allMethods = """ "showOnlyMethodsExceedingThreshold": false """;

        string Markdown(string name, params string[] extra)
        {
            var (exitCode, output) = Run([_dir, "-f", "Markdown", "-o", P(name), .. extra]);
            using (output)
            {
                Assert.That(exitCode, Is.EqualTo(0));
            }

            return File.ReadAllText(P(name));
        }

        string fromDefault = Markdown("default.md", "-c", WriteConfig(allMethods));
        string fromFlag = Markdown("flag.md", "-c", WriteConfig(allMethods), "--show-understandability");
        string fromConfig = Markdown("config.md", "-c", WriteConfig(allMethods + """, "showUnderstandability": true """));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fromDefault, Does.Not.Contain("Understandability"));
            // if +1, nested if +2, else +1, foreach +1.
            Assert.That(fromFlag, Does.Contain("| Understandability |").And.Contain("| 5 (low) |"));
            Assert.That(fromConfig, Is.EqualTo(fromFlag));
        }
    }

    // ---- report type ----

    [TestCase("html")]
    [TestCase("HTML")]
    [TestCase("Html")]
    public void ReportType_IsMatchedCaseInsensitively(string type)
    {
        var (exitCode, output) = Run(_dir, "-r", type, "-o", P("r.html"));
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(File.ReadAllText(P("r.html")), Does.Contain("<html"));
            Assert.That(output.Text + output.StdOut, Does.Contain("Html report generated: " + P("r.html")));
        }
    }

    [Test]
    public void ReportType_LowerCaseConsoleText_PrintsTheConsoleReport()
    {
        var (exitCode, output) = Run(_dir, "-r", "consoletext");
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text, Does.Contain("Summary:"));
        }
    }

    [Test]
    public void ReportType_Unknown_StillFailsAndListsTheSupportedTypes()
    {
        var (exitCode, output) = Run(_dir, "-r", "pdf", "-o", P("r.pdf"));
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(-1));
            Assert.That(output.Text + output.StdOut, Does.Contain("Unknown report type 'pdf'").And.Contain("Html"));
        }
    }

    // ---- console messages ----

    [Test]
    public void ConsoleReport_DoesNotClaimThatAReportFileWasGenerated()
    {
        var (exitCode, output) = Run(_dir);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text + output.StdOut, Does.Not.Contain("report generated"));
        }
    }

    [TestCase("Csv", "r.csv")]
    [TestCase("Markdown", "r.md")]
    [TestCase("Json", "r.json")]
    public void FileReports_StillAnnounceTheirFile(string type, string file)
    {
        var (exitCode, output) = Run(_dir, "-f", type, "-o", P(file));
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text + output.StdOut, Does.Contain($"{type} report generated: {P(file)}"));
        }
    }

    [Test]
    public void ConsoleReport_ShowsTheElseLoopsSwitchAndTryCatchColumns()
    {
        var (exitCode, output) = Run(_dir);
        using (output)
        {
            string header = output.Text.Split('\n').First(l => l.Contains("Method") && l.Contains("Score"));
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(
                Regex.Matches(header, "[A-Za-z/]+").Select(m => m.Value),
                Is.EqualTo(new[] { "Method", "Score", "Lines", "Ifs", "Else", "Loops", "Switch", "Try/Catch", "Arguments", "Nesting", "Returns", "Locals", "Fields", "Props" }));
        }
    }

    // ---- configuration ----

    [Test]
    public void UnknownMetricKey_InTheConfigFile_WarnsNamingTheKeyAndListsTheValidOnes_AndTheRunSucceeds()
    {
        string config = WriteConfig(""" "metrics": { "cyclomaticComplexity": { "threshold": 1, "scale": 1.0, "enabled": true } } """);

        var (exitCode, output) = Run(_dir, "-c", config);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            string text = output.Text + output.StdOut;
            Assert.That(text, Does.Contain("Warning: Unknown metric 'cyclomaticComplexity' in configuration is ignored. Valid metrics: linesOfCode, ifCount"));
            Assert.That(text, Does.Contain("Summary:"));
        }
    }

    [Test]
    public void DeprecatedMetricKeys_WarnButKeepWorking()
    {
        string config = WriteConfig(""" "metrics": { "variableCount": { "threshold": 0, "scale": 1.0, "enabled": true } } """);

        var (exitCode, output) = Run(_dir, "-c", config);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(
                output.Text + output.StdOut,
                Does.Contain("Warning: Metric key 'variableCount' is deprecated, use 'localVariableCount'."));
            Assert.That(output.Text, Does.Contain("Summary:"));
        }
    }

    [Test]
    public void ShippedAndGeneratedConfig_RunWithoutWarnings()
    {
        var (generated, o1) = Run("--generate-config", _dir);
        o1.Dispose();
        Assert.That(generated, Is.EqualTo(0));

        var (exitCode, output) = Run(_dir, "-c", P(ConfigurationResolver.DefaultFileName));
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text + output.StdOut, Does.Not.Contain("Warning"));
        }
    }

    [Test]
    public void GenerateConfig_WritesTheShippedFileVerbatim()
    {
        var (exitCode, output) = Run("--generate-config", _dir);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(
                File.ReadAllText(P(ConfigurationResolver.DefaultFileName)),
                Is.EqualTo(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, ConfigurationResolver.DefaultFileName))));
        }
    }

    [Test]
    public void ShowDetailedCognitiveMetricsFalse_HidesTheDetailColumnsInTheConsoleReport()
    {
        string config = WriteConfig(""" "showDetailedCognitiveMetrics": false, "showOnlyMethodsExceedingThreshold": false """);

        var (exitCode, output) = Run(_dir, "-c", config);
        using (output)
        {
            string header = output.Text.Split('\n').First(l => l.Contains("Method") && l.Contains("Score"));
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(Regex.Matches(header, "[A-Za-z/]+").Select(m => m.Value), Is.EqualTo(new[] { "Method", "Score" }));
        }
    }

    [Test]
    public void ShowDetailedCognitiveMetricsTrue_ShowsThem()
    {
        string config = WriteConfig(""" "showDetailedCognitiveMetrics": true, "showOnlyMethodsExceedingThreshold": false """);

        var (_, output) = Run(_dir, "-c", config);
        using (output)
        {
            Assert.That(output.Text, Does.Contain("Arguments"));
        }
    }

    [Test]
    public void ShowDetailedCognitiveMetricsFalse_AlsoAppliesToHtmlAndMarkdown()
    {
        string config = WriteConfig(""" "showDetailedCognitiveMetrics": false, "showOnlyMethodsExceedingThreshold": false """);

        var (_, o1) = Run(_dir, "-c", config, "-f", "Html", "-o", P("r.html"));
        o1.Dispose();
        var (_, o2) = Run(_dir, "-c", config, "-f", "Markdown", "-o", P("r.md"));
        o2.Dispose();

        Assert.That(File.ReadAllText(P("r.html")), Does.Not.Contain("<th>Arguments</th>").And.Contain("<th>Score</th>"));
        Assert.That(File.ReadAllText(P("r.md")), Does.Not.Contain("Arguments").And.Contain("| Method | Score |"));
    }

    // ---- coverage ----

    [Test]
    public void CoverageThatMatchesNothing_WarnsButSucceeds()
    {
        string path = P("elsewhere.xml");
        File.WriteAllText(path, """<coverage><packages><package name="P"><classes><class name="X" filename="/elsewhere/X.cs" lines-covered="1" lines-valid="2"/></classes></package></packages></coverage>""");

        var (exitCode, output) = Run(_dir, "--coverage-cobertura", path);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text + output.StdOut, Does.Contain("Warning: Loaded 1 coverage entries").And.Contain("none matched"));
        }
    }

    [Test]
    public void CoverageThatMatches_DoesNotWarn()
    {
        var (exitCode, output) = Run(_dir, "--coverage-cobertura", WriteCoverage());
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.Text + output.StdOut, Does.Not.Contain("Warning"));
        }
    }

    // ---- culture ----

    [Test]
    public void AllOutput_UnderGermanCulture_IsIdenticalToInvariantOutput()
    {
        string coverage = WriteCoverage();
        string[][] runs =
        [
            [_dir, "--coverage-cobertura", coverage, "--show-halstead", "--show-cyclomatic", "--show-coupling"],
            [_dir, "--coverage-cobertura", coverage, "-c", WriteConfig(""" "groupByClass": false """)],
            [_dir, "--coverage-cobertura", coverage, "-f", "Html", "-o", P("OUT.html")],
            [_dir, "--coverage-cobertura", coverage, "-f", "Markdown", "-o", P("OUT.md")],
            [_dir, "--coverage-cobertura", coverage, "-f", "Csv", "-o", P("OUT.csv")],
        ];

        string Snapshot(string[] args)
        {
            string[] files = [P("OUT.html"), P("OUT.md"), P("OUT.csv")];
            foreach (string f in files)
            {
                File.Delete(f);
            }

            var (exitCode, output) = Run(args);
            using (output)
            {
                Assert.That(exitCode, Is.EqualTo(0));
                // Progress lines are logged in the order the parallel phases finish; compare the report itself.
                string[] lines = output.Text.Split('\n');
                string console = string.Join("\n", lines.SkipWhile(l => !l.StartsWith("Class:", StringComparison.Ordinal)));
                return console + "|" + string.Join("|", files.Where(File.Exists).Select(File.ReadAllText));
            }
        }

        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo? previousDefault = CultureInfo.DefaultThreadCurrentCulture;
        try
        {
            string[] invariant = runs.Select(Snapshot).ToArray();

            var german = new CultureInfo("de-DE");
            CultureInfo.CurrentCulture = german;
            CultureInfo.DefaultThreadCurrentCulture = german;
            string[] underGerman = runs.Select(Snapshot).ToArray();

            for (int i = 0; i < runs.Length; i++)
            {
                Assert.That(underGerman[i], Is.EqualTo(invariant[i]), $"run {i}");
            }

            Assert.That(invariant[0], Does.StartWith("Class: Samples.Sample"));
            Assert.That(invariant[0], Does.Contain("Threshold: 0.500").And.Contain("Line: 75.0%").And.Contain("Branch: 33.3%"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.DefaultThreadCurrentCulture = previousDefault;
        }
    }
}
