using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class ReportMetricsFilterTests
{
    [Test]
    public void FilterForReport_WhenDisabled_ReturnsSameInstance()
    {
        var coll = new CognitiveMetricsCollection();
        var cfg = new CognitiveConfiguration { ShowOnlyMethodsExceedingThreshold = false };
        var r = ReportMetricsFilter.FilterForReport(coll, cfg);
        Assert.That(r, Is.SameAs(coll));
    }

    [Test]
    public void FilterForReport_WhenEnabled_ReturnsFiltered()
    {
        var m1 = new CognitiveMetrics("a", "C", "f.cs", "a()", 1) { totalScore = 1.0 };
        var m2 = new CognitiveMetrics("b", "C", "f.cs", "b()", 2) { totalScore = 9.0 };
        var coll = new CognitiveMetricsCollection { m1, m2 };
        var cfg = new CognitiveConfiguration { ShowOnlyMethodsExceedingThreshold = true, ScoreThreshold = 5.0 };
        var r = ReportMetricsFilter.FilterForReport(coll, cfg);
        Assert.That(r.Count, Is.EqualTo(1));
        Assert.That(r[0].MethodName, Is.EqualTo("b"));
    }
}
