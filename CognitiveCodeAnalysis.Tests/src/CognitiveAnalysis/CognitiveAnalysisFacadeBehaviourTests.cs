/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class CognitiveAnalysisFacadeBehaviourTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private static CognitiveAnalysisFacade Facade(CognitiveConfiguration? config = null) => new(
        new SourceFileFinder(),
        new CognitiveCodeAnalyser(),
        config ?? new CognitiveConfiguration(),
        new ScoreCalculator(),
        new AutoDetectCoverageReader(),
        new ClassCouplingAnalyser());

    private static CognitiveConfiguration ScoringConfig(bool coupling) => new()
    {
        ShowCouplingMetrics = coupling,
        Metrics = new Dictionary<string, MetricConfiguration>
        {
            ["ifCount"] = new() { Threshold = 0, Scale = 1, Enabled = true },
        },
    };

    private string Source(int methods)
    {
        var body = string.Concat(Enumerable.Range(0, methods).Select(i => $"public void M{i}() {{ if (true) {{ }} }}\n"));
        return _temp.CreateFileWithContent("Many.cs", $"public class Many {{ public Other O; {body} }} public class Other {{ }}");
    }

    [Test]
    public void AnalyseSourceFiles_ScoresEveryMethodAndReportsBatchedProgress()
    {
        var progress = new AnalysisProgressCollector();
        var config = ScoringConfig(coupling: false);

        var metrics = Facade(config).AnalyseSourceFiles([Source(250)], config, progress);

        Assert.That(metrics, Has.Count.EqualTo(250));
        Assert.That(metrics.All(m => Math.Abs(m.totalScore - Math.Log(2)) < 1e-12), Is.True);
        var scoring = progress.Reports.Where(r => r.Phase == AnalysisProgressPhase.CalculatingScores).ToList();
        Assert.That(scoring.First().ProcessedFiles, Is.EqualTo(0));
        Assert.That(scoring.Select(r => r.ProcessedFiles), Does.Contain(100).And.Contain(200).And.Contain(250));
        Assert.That(scoring.Select(r => r.ProcessedFiles).Where(p => p > 0), Is.All.Matches<int>(p => p % 100 == 0 || p == 250));
        Assert.That(scoring.All(r => r.TotalFiles == 250), Is.True);
        Assert.That(progress.Reports.Last(r => r.Phase == AnalysisProgressPhase.ScoresCalculated).ProcessedFiles, Is.EqualTo(250));
    }

    [Test]
    public void AnalyseSourceFiles_NoMethods_ReportsEmptyScoringPhases()
    {
        var progress = new AnalysisProgressCollector();
        var file = _temp.CreateFileWithContent("Empty.cs", "public class Empty { }");

        var metrics = Facade().AnalyseSourceFiles([file], new CognitiveConfiguration(), progress);

        Assert.That(metrics, Is.Empty);
        Assert.That(
            progress.Reports.Where(r => r.Phase is AnalysisProgressPhase.CalculatingScores or AnalysisProgressPhase.ScoresCalculated)
                .Select(r => (r.Phase, r.TotalFiles, r.ProcessedFiles)),
            Is.EqualTo(new[]
            {
                (AnalysisProgressPhase.CalculatingScores, 0, 0),
                (AnalysisProgressPhase.ScoresCalculated, 0, 0),
            }));
    }

    [Test]
    public void AnalyseSourceFiles_CouplingOnlyComputedWhenEnabled()
    {
        var file = Source(1);

        var without = Facade().AnalyseSourceFiles([file], ScoringConfig(coupling: false));
        var with = Facade().AnalyseSourceFiles([file], ScoringConfig(coupling: true));

        Assert.That(without.TryGetClassCoupling("Many", out _), Is.False);
        Assert.That(with.TryGetClassCoupling("Many", out ClassCouplingMetrics? coupling), Is.True);
        Assert.That(coupling!.OutgoingCoupling, Is.EqualTo(1));
    }

    [Test]
    public void AnalyseSourceFiles_DefaultsToConstructorConfiguration()
    {
        var file = Source(1);

        var metrics = Facade(ScoringConfig(coupling: false)).AnalyseSourceFiles([file]);

        Assert.That(metrics.Single().totalScore, Is.EqualTo(Math.Log(2)).Within(1e-12));
    }

    [Test]
    public void FindSourceFiles_ProgressOverloads_ReportSearchPhases()
    {
        _temp.CreateFile("A.cs");
        var progress = new AnalysisProgressCollector();

        var files = Facade().FindSourceFiles(_temp.tmpDirectory, progress);
        var viaArray = Facade().FindSourceFiles([_temp.tmpDirectory], progress);

        Assert.That(files, Has.Count.EqualTo(1));
        Assert.That(viaArray, Has.Count.EqualTo(1));
        Assert.That(progress.Reports.Select(r => r.Phase), Does.Contain(AnalysisProgressPhase.SearchCompleted));
    }

    private (CognitiveAnalysisFacade Facade, CognitiveMetricsCollection Metrics, string Source) AnalysedMethod()
    {
        var file = _temp.CreateFileWithContent("Calc.cs", "namespace N;\npublic class Calc\n{\n    public int Run(int a)\n    {\n        if (a > 0) { return 1; }\n        return 0;\n    }\n}\n");
        var facade = Facade(ScoringConfig(coupling: false));
        return (facade, facade.AnalyseSourceFiles([file]), file);
    }

    [Test]
    public void LoadCoverageData_MissingFile_ReportsNotFound()
    {
        var (facade, metrics, _) = AnalysedMethod();
        var missing = Path.Combine(_temp.tmpDirectory, "none.xml");

        var result = facade.LoadCoverageData(missing, metrics);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo($"Coverage file not found: {missing}"));
    }

    [Test]
    public void LoadCoverageData_UnparsableFile_ReportsFailure()
    {
        var (facade, metrics, _) = AnalysedMethod();
        var bad = _temp.CreateFileWithContent("bad.xml", "<unknown/>");

        var result = facade.LoadCoverageData(bad, metrics);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Does.StartWith("Failed to load coverage data: Unrecognized coverage XML format"));
    }

    [Test]
    public void LoadCoverageData_EmptyReport_ReportsNoData()
    {
        var (facade, metrics, _) = AnalysedMethod();
        var empty = _temp.CreateFileWithContent("empty.xml", "<coverage/>");

        var result = facade.LoadCoverageData(empty, metrics);

        Assert.That(result.Success, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("No coverage data found"));
    }

    [Test]
    public void LoadCoverageData_EntriesWithoutMatchingMethods_SucceedsAndLeavesMetricsUntouched()
    {
        var (facade, metrics, _) = AnalysedMethod();
        var other = _temp.CreateFileWithContent("other.xml",
            """<coverage><packages><package name="P"><classes><class name="X" filename="/elsewhere/X.cs" lines-covered="1" lines-valid="2"/></classes></package></packages></coverage>""");

        var result = facade.LoadCoverageData(other, metrics);

        Assert.That(result.Success, Is.True);
        Assert.That(result.WarningMessage, Does.Contain("Loaded 1 coverage entries").And.Contain("none matched"));
        Assert.That(metrics.Single().HasCoverageData, Is.False);
        Assert.That(metrics.Single().churnScore, Is.Null);
    }

    [Test]
    public void LoadCoverageData_MatchingMethod_SetsCoverageAndChurn()
    {
        var (facade, metrics, source) = AnalysedMethod();
        var coverage = _temp.CreateFileWithContent("cov.xml", $"""
            <coverage><sources><source>{Path.GetDirectoryName(source)}</source></sources><packages><package name="N"><classes>
              <class name="Calc" filename="Calc.cs" lines-covered="3" lines-valid="4" branches-covered="1" branches-valid="2">
                <methods>
                  <method name="Run" lines-covered="3" lines-valid="4" branches-covered="1" branches-valid="2">
                    <lines><line number="4" hits="1"/></lines>
                  </method>
                </methods>
              </class>
            </classes></package></packages></coverage>
            """);
        var progress = new AnalysisProgressCollector();

        var result = facade.LoadCoverageData(coverage, metrics, progress);

        var m = metrics.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WarningMessage, Is.Null);
            Assert.That(m.lineCoveragePercentage, Is.EqualTo(75.0));
            Assert.That(m.branchCoveragePercentage, Is.EqualTo(50.0));
            Assert.That(m.churnScore, Is.EqualTo(m.totalScore * 0.5).Within(1e-12));
            Assert.That(progress.Reports.Select(r => r.Phase), Does.Contain(AnalysisProgressPhase.CoverageApplied));
        }
    }

    [Test]
    public void LoadCoverageData_MethodWithoutBranches_HasNoBranchCoverageAndChurnUsesLineCoverage()
    {
        var (facade, metrics, source) = AnalysedMethod();
        var coverage = _temp.CreateFileWithContent("cov.xml", $"""
            <coverage><sources><source>{Path.GetDirectoryName(source)}</source></sources><packages><package name="N"><classes>
              <class name="Calc" filename="Calc.cs" lines-covered="4" lines-valid="4">
                <methods><method name="Run" lines-covered="4" lines-valid="4"><lines><line number="4" hits="1"/></lines></method></methods>
              </class>
            </classes></package></packages></coverage>
            """);

        facade.LoadCoverageData(coverage, metrics);

        var m = metrics.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(m.lineCoveragePercentage, Is.EqualTo(100.0));
            Assert.That(m.branchCoveragePercentage, Is.Null);
            Assert.That(m.HasCoverageData, Is.True);
            Assert.That(m.churnScore, Is.EqualTo(0.0));
        }
    }

    [Test]
    public void LoadCoverageData_MethodWithoutBranchesAndPartialLines_ChurnFollowsLineCoverage()
    {
        var (facade, metrics, source) = AnalysedMethod();
        var coverage = _temp.CreateFileWithContent("cov.xml", $"""
            <coverage><sources><source>{Path.GetDirectoryName(source)}</source></sources><packages><package name="N"><classes>
              <class name="Calc" filename="Calc.cs" lines-covered="1" lines-valid="4">
                <methods><method name="Run" lines-covered="1" lines-valid="4"><lines><line number="4" hits="1"/></lines></method></methods>
              </class>
            </classes></package></packages></coverage>
            """);

        facade.LoadCoverageData(coverage, metrics);

        var m = metrics.Single();
        Assert.That(m.branchCoveragePercentage, Is.Null);
        Assert.That(m.churnScore, Is.EqualTo(m.totalScore * 0.75).Within(1e-12));
    }

    [Test]
    public void LoadCoverageData_CoverletStyleRates_MatchByFirstCoveredLineWithinTheMethod()
    {
        var (facade, metrics, source) = AnalysedMethod();
        // Run spans lines 4-8; coverlet reports the first executable line (6), not the declaration line.
        var coverage = _temp.CreateFileWithContent("cov.xml", $"""
            <coverage line-rate="0.5" branch-rate="0.5"><sources><source>{Path.GetDirectoryName(source)}</source></sources><packages><package name="N"><classes>
              <class name="N.Calc" filename="Calc.cs" line-rate="0.1" branch-rate="0.1">
                <methods><method name="Run" line-rate="0.5" branch-rate="0.5"><lines>
                  <line number="6" hits="1" branch="True" condition-coverage="50% (1/2)"/>
                  <line number="7" hits="0" branch="False"/>
                </lines></method></methods>
              </class>
            </classes></package></packages></coverage>
            """);

        var result = facade.LoadCoverageData(coverage, metrics);

        var m = metrics.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.WarningMessage, Is.Null);
            Assert.That(m.lineCoveragePercentage, Is.EqualTo(50.0));
            Assert.That(m.branchCoveragePercentage, Is.EqualTo(50.0));
        }
    }

    [Test]
    public void LoadCoverageData_MethodNameMatchesButFirstLineIsOutsideTheMethod_DoesNotMatchTheMethod()
    {
        var (facade, metrics, source) = AnalysedMethod();
        var coverage = _temp.CreateFileWithContent("cov.xml", $"""
            <coverage><sources><source>{Path.GetDirectoryName(source)}</source></sources><packages><package name="N"><classes>
              <class name="Other" filename="Calc.cs" lines-covered="1" lines-valid="1">
                <methods><method name="Run" lines-covered="1" lines-valid="1"><lines><line number="40" hits="1"/></lines></method></methods>
              </class>
            </classes></package></packages></coverage>
            """);

        facade.LoadCoverageData(coverage, metrics);

        // The class-level fallback does not match "Other" either, so the file-level aggregate decides: none exists.
        Assert.That(metrics.Single().HasCoverageData, Is.False);
    }
}
