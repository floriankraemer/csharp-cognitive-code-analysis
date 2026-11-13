/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.RegularExpressions;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

/// <summary>CSV score column, Markdown alignment and baseline deltas, and the detail-column switch.</summary>
public class ReportParityFixTests
{
    private static string Render(IReport report, bool grouped, bool detailed = true, bool baseline = false)
    {
        var config = ReportGoldenFixtures.FullConfig(grouped);
        config.ShowDetailedCognitiveMetrics = detailed;
        return GoldenReportAssert.RenderReport(
            report,
            ReportGoldenFixtures.FullCollection(),
            config,
            GoldenNormalization.None,
            baseline ? ReportGoldenFixtures.FullBaselineComparison() : null);
    }

    [Test]
    public void Csv_AppendsLinesOfCodeScoreAfterAllExistingColumns()
    {
        string[] lines = Render(new CsvReport(), grouped: false).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        string[] header = SplitCsv(lines[0]);
        string[] alpha = SplitCsv(lines.Single(l => l.Contains(",Alpha,")));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(header[^1], Is.EqualTo("LinesOfCodeScore"));
            Assert.That(header[^2], Is.EqualTo("ChurnScore"));
            Assert.That(alpha[^1], Is.EqualTo("0.250"));
            Assert.That(header[Array.IndexOf(header, "LinesOfCode") + 1], Is.EqualTo("IfCount"));
        }
    }

    [Test]
    public void Csv_WithBaseline_AppendsTheScoreAndItsDeltaAtTheEnd()
    {
        string[] header = SplitCsv(Render(new CsvReport(), grouped: false, baseline: true).Split('\n')[0]);

        Assert.That(header[^2..], Is.EqualTo(new[] { "LinesOfCodeScore", "LinesOfCodeScoreDelta" }));
        Assert.That(header[^3], Is.EqualTo("ChurnScoreDelta"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Markdown_MethodColumnIsLeftAlignedAndTheNumbersAreRightAligned(bool grouped)
    {
        string md = Render(new MarkdownReport(), grouped);

        string separator = md.Split('\n').First(l => l.StartsWith("| :---", StringComparison.Ordinal));
        string[] cells = separator.Trim('|', ' ').Split(" | ");
        Assert.That(cells[0], Is.EqualTo(":---"));
        Assert.That(cells.Skip(1), Is.All.EqualTo("---:"));
        Assert.That(md.Split('\n').Where(l => l.StartsWith("| ---", StringComparison.Ordinal)), Is.Empty);
    }

    [Test]
    public void Markdown_WithBaseline_ShowsDeltasForChangedValuesOnly()
    {
        string md = Render(new MarkdownReport(), grouped: true, baseline: true);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(md, Does.Contain("| L10 Alpha | 8.500 ▲1.500 |"));
            Assert.That(md, Does.Contain("| L42 Beta | 0.600 ▼0.500 |"));
            Assert.That(md, Does.Contain("4 ▲3 (1.500)"));
            Assert.That(md, Does.Contain("**Coupling:** In=3 ▲2, Out=5, Stability=0.625 ▼0.175"));
            Assert.That(md, Does.Contain("120.46 ▲20.46"));
            Assert.That(md, Does.Contain("14.50 ▼2.00"));
            Assert.That(md, Does.Contain("0.900 ▲0.500"));
            // Gamma is unchanged: no arrows in its row.
            Assert.That(md.Split('\n').Single(l => l.Contains("L5 Gamma")), Does.Not.Contain("▲").And.Not.Contain("▼"));
        }
    }

    [Test]
    public void Markdown_WithoutBaseline_HasNoDeltas()
    {
        string md = Render(new MarkdownReport(), grouped: true);

        Assert.That(md, Does.Not.Contain("▲").And.Not.Contain("▼"));
    }

    [Test]
    public void Markdown_UngroupedWithBaseline_ShowsDeltasToo()
    {
        string md = Render(new MarkdownReport(), grouped: false, baseline: true);

        Assert.That(md, Does.Contain("| L10 Alpha | 8.500 ▲1.500 |"));
    }

    [Test]
    public void Markdown_MethodWithoutBaselineEntry_IsRenderedWithoutDeltas()
    {
        string md = Render(new MarkdownReport(), grouped: true, baseline: true);

        Assert.That(md.Split('\n').Single(l => l.Contains("L30 Delta")), Does.Not.Contain("▲").And.Not.Contain("▼"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Markdown_DetailedMetricsOff_HidesTheDetailColumns(bool grouped)
    {
        string md = Render(new MarkdownReport(), grouped, detailed: false);

        string header = md.Split('\n').First(l => l.StartsWith("| Method", StringComparison.Ordinal));
        Assert.That(header, Is.EqualTo(
            "| Method | Score | Halstead Volume | Halstead Difficulty | Halstead Effort | Cyclomatic Complexity | Churn | Line Coverage | Branch Coverage |"));
        Assert.That(md, Does.Not.Contain("4 (1.500)"));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Html_DetailedMetricsOff_HidesTheDetailColumnsAndCells(bool grouped)
    {
        string on = Render(new HtmlReport(), grouped);
        string off = Render(new HtmlReport(), grouped, detailed: false);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(on, Does.Contain("<th>Arguments</th>").And.Contain("<td>90 (0.250)</td>"));
            Assert.That(off, Does.Not.Contain("<th>Arguments</th>"));
            Assert.That(off, Does.Not.Contain("<th>Lines</th>"));
            Assert.That(off, Does.Not.Contain("<td>90 (0.250)</td>"));
            Assert.That(off, Does.Contain("<th>Score</th>").And.Contain("<th>Churn</th>").And.Contain("<th>Halstead Volume</th>"));
            Assert.That(off, Does.Contain("120.46"));
            Assert.That(CountCells(off, "<td>L10 Alpha</td>"), Is.EqualTo(CountHeaderCells(off)));
        }
    }

    [Test]
    public void ShowDetailedCognitiveMetrics_DoesNotAffectCsv()
    {
        string on = Render(new CsvReport(), grouped: false);
        string off = Render(new CsvReport(), grouped: false, detailed: false);

        Assert.That(off, Is.EqualTo(on));
    }

    // The method signature of the fixture contains a comma and is therefore quoted.
    private static string[] SplitCsv(string line)
        => Regex.Split(line, ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

    private static int CountHeaderCells(string html)
    {
        int start = html.IndexOf("<thead", StringComparison.Ordinal);
        int end = html.IndexOf("</thead>", start, StringComparison.Ordinal);
        return html[start..end].Split("<th>").Length - 1;
    }

    private static int CountCells(string html, string firstCell)
    {
        int start = html.IndexOf(firstCell, StringComparison.Ordinal);
        int end = html.IndexOf("</tr>", start, StringComparison.Ordinal);
        return html[start..end].Split("<td>").Length - 1;
    }
}
