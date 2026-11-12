/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

/// <summary>Colour thresholds, coupling line toggles and group ordering of the HTML and Markdown reports.</summary>
public class ReportBoundaryTests
{
    private static string Html(CognitiveMetricsCollection collection, CognitiveConfiguration? config = null,
        CognitiveBaselineComparison? baseline = null)
    {
        config ??= new CognitiveConfiguration { ShowOnlyMethodsExceedingThreshold = false };
        return GoldenReportAssert.RenderReport(new HtmlReport(), collection, config, GoldenNormalization.None, baseline);
    }

    private static string Markdown(CognitiveMetricsCollection collection, CognitiveConfiguration config)
        => GoldenReportAssert.RenderReport(new MarkdownReport(), collection, config);

    private static CognitiveMetrics Metric(string cls, string name, double score, string file = "a.cs") =>
        new(name, cls, file, $"void {name}()", 1) { totalScore = score };

    [TestCase(0.49, "score-green")]
    [TestCase(0.5, "score-yellow")]
    [TestCase(0.84, "score-yellow")]
    [TestCase(0.85, "score-red")]
    [TestCase(3.0, "score-red")]
    public void Html_ScoreColourBands(double score, string expectedClass)
    {
        var html = Html([Metric("C", "M", score)]);

        Assert.That(html, Does.Contain($"<span class=\"{expectedClass}\">{score:F3}</span>"));
    }

    [TestCase(0.29, "score-green")]
    [TestCase(0.3, "score-yellow")]
    [TestCase(0.7, "score-yellow")]
    [TestCase(0.71, "score-red")]
    public void Html_ChurnColourBands(double churn, string expectedClass)
    {
        var m = Metric("C", "M", 0.0);
        m.churnScore = churn;
        m.lineCoveragePercentage = 50;

        var html = Html([m]);

        Assert.That(html, Does.Contain($"<span class=\"{expectedClass}\">{churn:F3}</span>"));
    }

    [Test]
    public void Html_MissingChurn_ShowsNotAvailableInGreen()
    {
        var m = Metric("C", "M", 0.0);
        m.lineCoveragePercentage = 50;

        Assert.That(Html([m]), Does.Contain("<span class=\"score-green\">n/a</span>"));
    }

    [Test]
    public void Html_GroupedOrdersClassesByHighestScoreThenName_AndMethodsByScore()
    {
        var collection = new CognitiveMetricsCollection
        {
            Metric("Zed", "Low", 1.0, "z.cs"),
            Metric("Beta", "Mid", 2.0, "b.cs"),
            Metric("Alpha", "Mid2", 2.0, "a.cs"),
            Metric("Zed", "High", 9.0, "z.cs"),
        };
        var config = new CognitiveConfiguration { ShowOnlyMethodsExceedingThreshold = false, GroupByClass = true };

        var html = Html(collection, config);

        int zed = html.IndexOf("Class: Zed", StringComparison.Ordinal);
        int alpha = html.IndexOf("Class: Alpha", StringComparison.Ordinal);
        int beta = html.IndexOf("Class: Beta", StringComparison.Ordinal);
        Assert.That(zed, Is.LessThan(alpha));
        Assert.That(alpha, Is.LessThan(beta));
        Assert.That(html.IndexOf("High", StringComparison.Ordinal), Is.LessThan(html.IndexOf("Low", StringComparison.Ordinal)));
    }

    [Test]
    public void Markdown_GroupedOrdersClassesByHighestScoreThenName()
    {
        var collection = new CognitiveMetricsCollection
        {
            Metric("Zed", "Low", 1.0, "z.cs"),
            Metric("Beta", "Mid", 2.0, "b.cs"),
            Metric("Alpha", "Mid2", 2.0, "a.cs"),
            Metric("Zed", "High", 9.0, "z.cs"),
        };
        var config = new CognitiveConfiguration { ShowOnlyMethodsExceedingThreshold = false, GroupByClass = true };

        var md = Markdown(collection, config);

        Assert.That(md.IndexOf("### Class: Zed", StringComparison.Ordinal), Is.LessThan(md.IndexOf("### Class: Alpha", StringComparison.Ordinal)));
        Assert.That(md.IndexOf("### Class: Alpha", StringComparison.Ordinal), Is.LessThan(md.IndexOf("### Class: Beta", StringComparison.Ordinal)));
    }

    private static CognitiveMetricsCollection WithCoupling()
    {
        var c = new CognitiveMetricsCollection { Metric("Svc", "M", 1.0), Metric("NoCoupling", "N", 1.0, "n.cs") };
        c.SetClassCouplingMetrics([new ClassCouplingMetrics { ClassName = "Svc", IncomingCoupling = 2, OutgoingCoupling = 3, Stability = 0.4 }]);
        return c;
    }

    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, false)]
    public void CouplingLine_OnlyWhenGroupedAndEnabled(bool grouped, bool showCoupling, bool expected)
    {
        var config = new CognitiveConfiguration
        {
            ShowOnlyMethodsExceedingThreshold = false, GroupByClass = grouped, ShowCouplingMetrics = showCoupling,
        };

        Assert.That(Html(WithCoupling(), config).Contains("Coupling:", StringComparison.Ordinal), Is.EqualTo(expected));
        Assert.That(Markdown(WithCoupling(), config).Contains("**Coupling:**", StringComparison.Ordinal), Is.EqualTo(expected));
    }

    [Test]
    public void CouplingLine_ClassWithoutCouplingData_ShowsNotAvailable()
    {
        var config = new CognitiveConfiguration { ShowOnlyMethodsExceedingThreshold = false, GroupByClass = true, ShowCouplingMetrics = true };

        Assert.That(Html(WithCoupling(), config), Does.Contain("Coupling: n/a"));
        Assert.That(Markdown(WithCoupling(), config), Does.Contain("**Coupling:** n/a"));
        Assert.That(Html(WithCoupling(), config), Does.Contain("Coupling: In=2, Out=3, Stability=0.400"));
        Assert.That(Markdown(WithCoupling(), config), Does.Contain("**Coupling:** In=2, Out=3, Stability=0.400"));
    }

    [Test]
    public void Html_CouplingDeltasAreShownOnlyForClassesWithABaseline()
    {
        var config = ReportGoldenFixtures.FullConfig(groupByClass: true);

        var html = Html(ReportGoldenFixtures.FullCollection(), config, ReportGoldenFixtures.FullBaselineComparison());

        // Svc has a baseline (in 1 -> 3, stability 0.8 -> 0.625); Repo has none.
        Assert.That(html, Does.Contain("In=3 <span class=\"delta-up\">▲2</span>, Out=5, Stability=0.625 <span class=\"delta-down\">▼0.175</span>"));
        Assert.That(html, Does.Contain("In=1, Out=0, Stability=0.000"));
    }

    [Test]
    public void Html_EscapesMarkupInNames()
    {
        var m = new CognitiveMetrics("M<T>", "C&D", "a\"b.cs", "List<int> M<T>()", 7) { totalScore = 1 };

        var html = Html([m]);

        Assert.That(html, Does.Contain("C&amp;D"));
        Assert.That(html, Does.Contain("M&lt;T&gt;"));
        Assert.That(html, Does.Contain("a&quot;b.cs"));
    }

    [Test]
    public void Html_FilterScriptIsPresentOnlyWhenThereAreSections()
    {
        var config = new CognitiveConfiguration { ShowOnlyMethodsExceedingThreshold = false };

        Assert.That(Html([Metric("C", "M", 1)], config), Does.Contain("report-filter"));
    }

    [Test]
    public void Html_EmptyCollection_StillProducesADocument()
    {
        var html = Html([]);

        Assert.That(html, Does.Contain("</html>"));
        Assert.That(html, Does.Not.Contain("report-class-section\" data"));
    }
}
