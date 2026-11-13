/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;
using CognitiveCodeAnalysis.HalsteadAnalysis;
using CognitiveCodeAnalysisConsoleApp.CognitiveAnalysis.Reports;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Reports;

public class ConsoleTextReportTests
{
    private static CognitiveMetrics Metric(string cls, string name, int line, double score, string file = "src/A.cs") =>
        new(name, cls, file, $"void {name}()", line) { totalScore = score, linesOfCode = 12 };

    private static CognitiveMetricsCollection Collection(double alpha = 8.5, double beta = 0.6, double gamma = 0.2)
    {
        var a = Metric("Svc", "Alpha", 10, alpha);
        a.ifCount = 4;
        a.ifScore = 1.5;
        a.cyclomaticComplexity = 7;
        a.Halstead = new HalsteadMetrics { Volume = 120.456, Difficulty = 14.5, Effort = 1746.6 };
        a.lineCoveragePercentage = 80;
        a.branchCoveragePercentage = 49.9;
        a.churnScore = 0.9;

        var b = Metric("Svc", "Beta", 42, beta);
        b.lineCoveragePercentage = 55;
        b.churnScore = 0.5;

        var c = Metric("Repo", "Gamma", 5, gamma, "src/B.cs");
        c.branchCoveragePercentage = 100;
        c.churnScore = 0.1;

        var d = Metric("Repo", "Delta", 30, 0.45, "src/B.cs");

        var collection = new CognitiveMetricsCollection { a, b, c, d };
        collection.SetClassCouplingMetrics(
        [
            new ClassCouplingMetrics { ClassName = "Svc", IncomingCoupling = 3, OutgoingCoupling = 5, Stability = 0.625 },
        ]);
        return collection;
    }

    private static CognitiveConfiguration Config(bool grouped, bool full = true) => new()
    {
        ScoreThreshold = 5.0,
        ShowOnlyMethodsExceedingThreshold = false,
        GroupByClass = grouped,
        ShowCouplingMetrics = full,
        ShowHalsteadComplexity = full,
        ShowCyclomaticComplexity = full,
    };

    private static CognitiveBaselineComparison Baseline()
    {
        var baseline = Collection(alpha: 7.0, beta: 1.1);
        baseline.Remove(baseline.Single(m => m.MethodName == "Delta"));
        baseline.Single(m => m.MethodName == "Alpha").churnScore = 0.4;
        baseline.SetClassCouplingMetrics(
        [
            new ClassCouplingMetrics { ClassName = "Svc", IncomingCoupling = 1, OutgoingCoupling = 5, Stability = 0.8 },
        ]);
        return BaselineComparer.Compare(Collection(), BaselineSnapshotFactory.FromMetricsCollection(baseline));
    }

    private static string Render(
        CognitiveMetricsCollection collection,
        CognitiveConfiguration config,
        CognitiveBaselineComparison? baseline = null)
    {
        using var capture = new ConsoleOutputCapture();
        new ConsoleTextReport().RenderMetrics(string.Empty, collection, config, baseline);
        return capture.Text;
    }

    [Test]
    public void Name_IsConsoleText() => Assert.That(new ConsoleTextReport().Name, Is.EqualTo("ConsoleText"));

    [Test]
    public void Grouped_PrintsClassHeaderCouplingTablesAndSummary()
    {
        string text = Render(Collection(), Config(grouped: true));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("Class: Svc"));
            Assert.That(text, Does.Contain("Class: Repo"));
            Assert.That(text, Does.Contain("File: src/A.cs"));
            Assert.That(text, Does.Contain("Coupling: In=3, Out=5, Stability=0.625"));
            Assert.That(text, Does.Contain("Coupling: n/a"));
            Assert.That(text, Does.Contain("L10 Alpha"));
            Assert.That(text, Does.Contain("8.500"));
            Assert.That(text, Does.Contain("4 (1.500)"));
            Assert.That(text, Does.Contain("Halstead Vol"));
            Assert.That(text, Does.Contain("120.46"));
            Assert.That(text, Does.Contain("Cyclomatic"));
            Assert.That(text, Does.Contain("7.0"));
            Assert.That(text, Does.Contain("Line: 80.0%"));
            Assert.That(text, Does.Contain("Branch: 49.9%"));
            Assert.That(text, Does.Contain("0.900"));
            Assert.That(text, Does.Contain("Summary:"));
            Assert.That(text, Does.Contain("Total Classes Processed: 2"));
            Assert.That(text, Does.Contain("Total Methods Processed: 4"));
            Assert.That(text, Does.Contain("Methods Exceeding Threshold: 1 (25.0%)"));
            Assert.That(text, Does.Contain("Classes with Methods Exceeding Threshold: 1 (50.0%)"));
            Assert.That(text, Does.Contain("Threshold: 5.000"));
        }
    }

    [Test]
    public void Grouped_OrdersClassesByName()
    {
        string text = Render(Collection(), Config(grouped: true));

        Assert.That(text.IndexOf("Class: Repo", StringComparison.Ordinal), Is.LessThan(text.IndexOf("Class: Svc", StringComparison.Ordinal)));
    }

    [Test]
    public void Ungrouped_PrintsClassMethodAndFilePerMetric()
    {
        string text = Render(Collection(), Config(grouped: false));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("Class: Svc"));
            Assert.That(text, Does.Contain("Method: void Alpha()"));
            Assert.That(text, Does.Contain("File: src/B.cs"));
            Assert.That(text, Does.Not.Contain("Coupling:"));
            Assert.That(text, Does.Contain("Summary:"));
        }
    }

    [Test]
    public void OptionalColumns_AreOmittedWhenDisabledAndNoCoverage()
    {
        var plain = new CognitiveMetricsCollection { Metric("C", "M", 1, 1.0) };

        string text = Render(plain, Config(grouped: true, full: false));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Not.Contain("Halstead"));
            Assert.That(text, Does.Not.Contain("Cyclomatic"));
            Assert.That(text, Does.Not.Contain("Coverage"));
            Assert.That(text, Does.Not.Contain("Churn"));
            Assert.That(text, Does.Not.Contain("Coupling:"));
        }
    }

    [Test]
    public void CoverageAndChurnColumns_ShownWhenAnyMethodHasCoverage_WithNotAvailableMarkers()
    {
        string text = Render(Collection(), Config(grouped: true, full: false));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("Coverage"));
            Assert.That(text, Does.Contain("Churn"));
            Assert.That(text, Does.Contain("n/a"));
            Assert.That(text, Does.Contain("Line: 55.0%"));
            Assert.That(text, Does.Contain("Branch: 100.0%"));
        }
    }

    [Test]
    public void MissingHalstead_ShowsNotAvailable()
    {
        var m = new CognitiveMetricsCollection { Metric("C", "M", 1, 1.0) };

        string text = Render(m, Config(grouped: true));

        Assert.That(text, Does.Contain("n/a"));
    }

    [Test]
    public void ShowOnlyExceeding_FiltersRowsButSummaryCoversAllMethods()
    {
        var config = Config(grouped: true);
        config.ShowOnlyMethodsExceedingThreshold = true;

        string text = Render(Collection(), config);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("L10 Alpha"));
            Assert.That(text, Does.Not.Contain("L42 Beta"));
            Assert.That(text, Does.Not.Contain("Class: Repo"));
            Assert.That(text, Does.Contain("Total Methods Processed: 4"));
        }
    }

    [Test]
    public void EmptyCollection_PrintsNothingAndNoSummary()
    {
        Assert.That(Render(new CognitiveMetricsCollection(), Config(grouped: true)).Trim(), Is.Empty);
        Assert.That(Render(new CognitiveMetricsCollection(), Config(grouped: false)).Trim(), Is.Empty);
    }

    [Test]
    public void BaselineDeltas_ArePrintedForChangedValuesOnly()
    {
        string text = Render(Collection(), Config(grouped: true), Baseline());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("▲1.500"));
            Assert.That(text, Does.Contain("▼0.500"));
            Assert.That(text, Does.Contain("In=3 ▲2"));
            Assert.That(text, Does.Contain("Stability=0.625 ▼0.175"));
        }
    }

    [Test]
    public void BaselineDeltas_UngroupedAndNoCouplingBaseline()
    {
        string text = Render(Collection(), Config(grouped: false), Baseline());

        Assert.That(text, Does.Contain("▲1.500"));
    }

    [Test]
    public void MarkupInNames_IsEscapedNotInterpreted()
    {
        var m = Metric("List[T]", "Do[x]", 3, 1.0, "src/[a].cs");

        string text = Render(new CognitiveMetricsCollection { m }, Config(grouped: true, full: false));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(text, Does.Contain("Class: List[T]"));
            Assert.That(text, Does.Contain("L3 Do[x]"));
            Assert.That(text, Does.Contain("File: src/[a].cs"));
        }
    }

    [Test]
    public void Progress_IsReportedPerRenderedMethod()
    {
        var reports = new List<AnalysisProgress>();
        using var capture = new ConsoleOutputCapture();

        new ConsoleTextReport().RenderMetrics(string.Empty, Collection(), Config(grouped: true), null, new SyncProgress(reports));

        Assert.That(reports.Count(r => r.Phase == AnalysisProgressPhase.WritingReport), Is.GreaterThanOrEqualTo(4));
        Assert.That(reports.Last().Phase, Is.EqualTo(AnalysisProgressPhase.ReportCompleted));
    }

    [TestCase(0.5, 0.0, "green")]
    [TestCase(0.5, 0.5, "green")]
    [TestCase(0.5, 0.51, "yellow")]
    [TestCase(0.5, 1.0, "yellow")]
    [TestCase(0.5, 1.01, "red")]
    [TestCase(5.0, 5.0, "green")]
    [TestCase(5.0, 9.99, "yellow")]
    [TestCase(5.0, 10.01, "red")]
    [TestCase(0.0, 0.0, "green")]
    [TestCase(0.0, 0.01, "red")]
    public void ScoreColour_FollowsTheScoreThreshold(double threshold, double score, string colour)
    {
        string scoreText = score.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        Assert.That(ConsoleTextReport.ColorizeScore(score, threshold), Is.EqualTo($"[{colour}]{scoreText}[/]"));
    }

    [Test]
    public void Columns_ContainElseLoopsSwitchAndTryCatch_InTheSameOrderAsTheOtherReports()
    {
        var m = Metric("C", "M", 1, 1.0);
        m.elseCount = 2;
        m.loopCount = 3;
        m.switchCount = 4;
        m.tryCatchCount = 5;

        string text = Render(new CognitiveMetricsCollection { m }, Config(grouped: true, full: false));

        string header = text.Split('\n').First(l => l.Contains("Method"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(
                System.Text.RegularExpressions.Regex.Matches(header, "[A-Za-z/]+").Select(x => x.Value),
                Is.EqualTo(new[] { "Method", "Score", "Lines", "Ifs", "Else", "Loops", "Switch", "Try/Catch", "Arguments", "Nesting", "Returns", "Locals", "Fields", "Props" }));
            Assert.That(text, Does.Contain("2 (0.000)").And.Contain("3 (0.000)").And.Contain("4 (0.000)").And.Contain("5 (0.000)"));
        }
    }

    [Test]
    public void DetailedMetricsOff_KeepsOnlyMethodScoreAndOptionalColumns()
    {
        var config = Config(grouped: true);
        config.ShowDetailedCognitiveMetrics = false;

        string text = Render(Collection(), config);

        string header = text.Split('\n').First(l => l.Contains("Method"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(header, Does.Not.Contain("Lines").And.Not.Contain("Arguments").And.Not.Contain("Props"));
            Assert.That(header, Does.Contain("Score").And.Contain("Halstead Vol").And.Contain("Cyclomatic").And.Contain("Coverage").And.Contain("Churn"));
            Assert.That(text, Does.Contain("L10 Alpha").And.Contain("8.500").And.Contain("120.46"));
            Assert.That(text, Does.Not.Contain("4 (1.500)"));
        }
    }

    [Test]
    public void Ungrouped_ListsTheMostComplexMethodsFirst_AndKeepsTheInputOrderForTies()
    {
        var low = Metric("C", "Low", 1, 1.0);
        var tieA = Metric("C", "TieA", 2, 2.0);
        var tieB = Metric("C", "TieB", 3, 2.0);
        var high = Metric("C", "High", 4, 3.0);

        string text = Render(new CognitiveMetricsCollection { low, tieA, tieB, high }, Config(grouped: false, full: false));

        var order = new[] { "L4 High", "L2 TieA", "L3 TieB", "L1 Low" }.Select(s => text.IndexOf(s, StringComparison.Ordinal)).ToArray();
        Assert.That(order, Is.Ordered.And.All.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void Numbers_UnderGermanCulture_KeepTheInvariantFormat()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");

            string text = Render(Collection(), Config(grouped: true));

            using (Assert.EnterMultipleScope())
            {
                Assert.That(text, Does.Contain("8.500").And.Contain("Line: 80.0%").And.Contain("Branch: 49.9%").And.Contain("0.900"));
                Assert.That(text, Does.Contain("(25.0%)").And.Contain("Threshold: 5.000"));
                Assert.That(text, Does.Not.Match(@"\d,\d"));
            }
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    private static string RenderAt(int width, bool interactive, CognitiveConfiguration config)
    {
        using var capture = new ConsoleOutputCapture(width, interactive);
        new ConsoleTextReport().RenderMetrics(string.Empty, Collection(), config, null);
        return capture.Text;
    }

    private static string RedirectedGoldenPath(string name)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", name));

    [TestCase(true, "console.redirected.grouped.txt")]
    [TestCase(false, "console.redirected.ungrouped.txt")]
    public void RedirectedOutput_IsRenderedWideAndMatchesTheGoldenFile(bool grouped, string golden)
    {
        // Spectre assumes 80 columns when stdout is a pipe or a file.
        string actual = RenderAt(80, interactive: false, Config(grouped));

        if (Environment.GetEnvironmentVariable("UPDATE_GOLDEN") == "1")
        {
            File.WriteAllText(RedirectedGoldenPath(golden), actual);
            return;
        }

        Assert.That(actual, Is.EqualTo(File.ReadAllText(RedirectedGoldenPath(golden)).Replace("\r\n", "\n", StringComparison.Ordinal)));
    }

    [Test]
    public void RedirectedOutput_KeepsEveryTableRowOnOneLine()
    {
        string text = RenderAt(80, interactive: false, Config(grouped: true));

        string[] lines = text.Split('\n');
        string header = lines.First(l => l.Contains("Method") && l.Contains("Props") && l.Contains("Churn"));
        string alpha = lines.Single(l => l.Contains("L10 Alpha"));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(header, Does.Contain("Score").And.Contain("Else").And.Contain("Try/Catch"));
            Assert.That(alpha, Does.Contain("8.500").And.Contain("4 (1.500)").And.Contain("120.46"));
            Assert.That(lines.Max(l => l.Length), Is.LessThanOrEqualTo(ConsoleTextReport.RedirectedWidth));
        }
    }

    [Test]
    public void InteractiveTerminal_KeepsItsRealWidth()
    {
        string text = RenderAt(80, interactive: true, Config(grouped: true));

        Assert.That(text.Split('\n').Max(l => l.Length), Is.LessThanOrEqualTo(80));
    }

    [Test]
    public void TheWidthOfTheConsole_IsRestoredAfterRendering()
    {
        using var capture = new ConsoleOutputCapture(width: 80, interactive: false);

        new ConsoleTextReport().RenderMetrics(string.Empty, Collection(), Config(grouped: true), null);

        Assert.That(Spectre.Console.AnsiConsole.Profile.Width, Is.EqualTo(80));
    }

    [Test]
    public void AnAlreadyWiderConsole_IsNotNarrowed()
    {
        using var capture = new ConsoleOutputCapture(width: 500, interactive: false);

        new ConsoleTextReport().RenderMetrics(string.Empty, Collection(), Config(grouped: true), null);

        Assert.That(Spectre.Console.AnsiConsole.Profile.Width, Is.EqualTo(500));
    }

    private sealed class SyncProgress(List<AnalysisProgress> sink) : IProgress<AnalysisProgress>
    {
        public void Report(AnalysisProgress value) => sink.Add(value);
    }
}
