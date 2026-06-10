/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Baseline;

public class CognitiveReportDeltaFormatterTests
{
    [Test]
    public void FormatHtmlDeltaSuffix_Up_IsRedWithTriangle()
    {
        var delta = new MetricDelta { BaselineValue = 2.0, CurrentValue = 2.5, Delta = 0.5 };
        var suffix = CognitiveReportDeltaFormatter.FormatHtmlDeltaSuffix(delta, "F3");

        Assert.That(suffix, Does.Contain("delta-up"));
        Assert.That(suffix, Does.Contain("▲0.500"));
    }

    [Test]
    public void FormatHtmlDeltaSuffix_Down_IsGreenWithTriangle()
    {
        var delta = new MetricDelta { BaselineValue = 2.0, CurrentValue = 1.5, Delta = -0.5 };
        var suffix = CognitiveReportDeltaFormatter.FormatHtmlDeltaSuffix(delta, "F3");

        Assert.That(suffix, Does.Contain("delta-down"));
        Assert.That(suffix, Does.Contain("▼0.500"));
    }

    [Test]
    public void FormatHtmlDeltaSuffix_Zero_IsEmpty()
    {
        var delta = new MetricDelta { BaselineValue = 2.0, CurrentValue = 2.0, Delta = 0.0 };
        Assert.That(CognitiveReportDeltaFormatter.FormatHtmlDeltaSuffix(delta, "F3"), Is.Empty);
    }

    [Test]
    public void FormatConsoleDeltaSuffix_Up_IsRedMarkup()
    {
        var delta = new MetricDelta { BaselineValue = 2.0, CurrentValue = 3.0, Delta = 1.0 };
        var suffix = CognitiveReportDeltaFormatter.FormatConsoleDeltaSuffix(delta, "F3");

        Assert.That(suffix, Does.Contain("[red]"));
        Assert.That(suffix, Does.Contain("▲1.000"));
    }

    [Test]
    public void FormatCiSuffix_IncludesBaselineText()
    {
        var delta = new MetricDelta { BaselineValue = 2.0, CurrentValue = 2.5, Delta = 0.5 };
        var suffix = CognitiveReportDeltaFormatter.FormatCiSuffix(delta, "F3");

        Assert.That(suffix, Is.EqualTo(" (▲0.500 vs baseline)"));
    }

    [Test]
    public void FormatCsvDelta_ReturnsSignedValue()
    {
        var delta = new MetricDelta { BaselineValue = 2.0, CurrentValue = 2.5, Delta = 0.5 };
        Assert.That(CognitiveReportDeltaFormatter.FormatCsvDelta(delta, "F3"), Is.EqualTo("0.500"));
    }
}
