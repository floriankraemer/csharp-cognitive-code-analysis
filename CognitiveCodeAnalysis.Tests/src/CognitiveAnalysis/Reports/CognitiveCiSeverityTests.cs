/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.HalsteadAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class CognitiveCiSeverityTests
{
    private static CognitiveMetrics Metric(double score) =>
        new("Run", "Svc", "a.cs", "void Run()", 3) { totalScore = score };

    private static CognitiveConfiguration Config() => new() { ScoreThreshold = 5.0 };

    [TestCase(5.1, "warning", "minor", "warning")]
    [TestCase(5.0, "note", "info", "notice")]
    [TestCase(4.9, "note", "info", "notice")]
    public void Severities_AboveThresholdIsWarning_AtOrBelowIsInformational(
        double score, string sarif, string gitlab, string github)
    {
        var m = Metric(score);
        var c = Config();

        Assert.That(CognitiveCiSeverity.SarifLevel(m, c), Is.EqualTo(sarif));
        Assert.That(CognitiveCiSeverity.GitlabSeverity(m, c), Is.EqualTo(gitlab));
        Assert.That(CognitiveCiSeverity.GithubCommandKind(m, c), Is.EqualTo(github));
    }

    [Test]
    public void BuildMessage_Basic_ListsAllTwelveSegmentsWithoutTrailingSeparator()
    {
        var message = CognitiveCiSeverity.BuildMessage(Metric(6.0), Config());

        Assert.That(message, Does.StartWith("Cognitive complexity score 6.000 for Svc.Run (threshold 5.000). lines=0(0.000); if=0(0.000);"));
        Assert.That(message, Does.EndWith("props=0(0.000)"));
        Assert.That(message, Does.Not.Contain("halstead").And.Not.Contain("cyclomatic").And.Not.Contain("coverage"));
    }

    [Test]
    public void BuildMessage_WithOptions_AppendsHalsteadCyclomaticCoverageAndChurn()
    {
        var m = Metric(6.0);
        m.Halstead = new HalsteadMetrics { Volume = 10.126, Difficulty = 2, Effort = 3 };
        m.cyclomaticComplexity = 4;
        m.lineCoveragePercentage = 75.56;
        m.branchCoveragePercentage = 50;
        m.churnScore = 1.5;
        var c = Config();
        c.ShowHalsteadComplexity = true;
        c.ShowCyclomaticComplexity = true;

        var message = CognitiveCiSeverity.BuildMessage(m, c);

        Assert.That(message, Does.EndWith(
            "props=0(0.000); halstead-vol=10.13; halstead-diff=2.00; halstead-effort=3.00; cyclomatic=4.0; "
            + "line-coverage=75.6%; branch-coverage=50.0%; churn=1.500"));
    }

    [Test]
    public void BuildMessage_OnlyBranchCoverage_OmitsLineCoverage()
    {
        var m = Metric(6.0);
        m.branchCoveragePercentage = 10;

        var message = CognitiveCiSeverity.BuildMessage(m, Config());

        Assert.That(message, Does.Contain("branch-coverage=10.0%"));
        Assert.That(message, Does.Not.Contain("line-coverage"));
        Assert.That(message, Does.Not.Contain("churn"));
    }

    [Test]
    public void BuildMessage_OnlyLineCoverage_OmitsBranchCoverage()
    {
        var m = Metric(6.0);
        m.lineCoveragePercentage = 10;

        var message = CognitiveCiSeverity.BuildMessage(m, Config());

        Assert.That(message, Does.Contain("line-coverage=10.0%"));
        Assert.That(message, Does.Not.Contain("branch-coverage"));
    }

    [Test]
    public void BuildMessage_HalsteadEnabledButMissing_OmitsHalstead()
    {
        var c = Config();
        c.ShowHalsteadComplexity = true;

        Assert.That(CognitiveCiSeverity.BuildMessage(Metric(1), c), Does.Not.Contain("halstead"));
    }

    [Test]
    public void BuildMessage_WithBaselineComparison_AppendsDelta()
    {
        var current = Metric(6.0);
        var baselineMetric = Metric(5.0);
        var config = Config();
        var comparison = BaselineComparer.Compare(
            new CognitiveMetricsCollection { current },
            BaselineSnapshotFactory.FromMetricsCollection(new CognitiveMetricsCollection { baselineMetric }));

        var message = CognitiveCiSeverity.BuildMessage(current, config, comparison);

        Assert.That(message, Does.EndWith(" (▲1.000 vs baseline)"));
    }

    [Test]
    public void BuildMessage_BaselineWithoutMatchingMethod_HasNoDeltaSuffix()
    {
        var other = new CognitiveMetrics("Other", "Svc", "a.cs", "void Other()", 9) { totalScore = 1 };
        var config = Config();
        var comparison = BaselineComparer.Compare(
            new CognitiveMetricsCollection { Metric(6.0) },
            BaselineSnapshotFactory.FromMetricsCollection(new CognitiveMetricsCollection { other }));

        var message = CognitiveCiSeverity.BuildMessage(Metric(6.0), config, comparison);

        Assert.That(message, Does.Not.Contain("baseline"));
    }
}
