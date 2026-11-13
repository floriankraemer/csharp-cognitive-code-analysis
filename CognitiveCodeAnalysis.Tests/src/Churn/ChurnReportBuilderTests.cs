/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.Churn;

namespace CognitiveCodeAnalysis.Tests.Churn;

public class ChurnReportBuilderTests
{
    private readonly ChurnReportBuilder _builder = new(FixedTimeProvider.Default);

    /// <summary>The data of the PHP reference implementation's golden files (same names, same numbers).</summary>
    private static List<ChurnMetrics> Sample() =>
    [
        new(@"Phauthentic\CognitiveCodeAnalysis\Business\Cognitive\CognitiveMetrics",
            "/home/florian/projects/cognitive-code-checker/src/Business/Cognitive/CognitiveMetrics.php", 2.042, 6, 12.252),
        new(@"Phauthentic\CognitiveCodeAnalysis\Command\Presentation\CognitiveMetricTextRenderer",
            "/home/florian/projects/cognitive-code-checker/src/Command/Presentation/CognitiveMetricTextRenderer.php", 0.806, 10, 8.06),
        new(@"Phauthentic\CognitiveCodeAnalysis\Business\MetricsFacade",
            "/home/florian/projects/cognitive-code-checker/src/Business/MetricsFacade.php", 0.693, 8, 5.544),
    ];

    /// <summary>One class per risk level, one that is covered 0 percent, and one that coverage does not know.</summary>
    private static List<ChurnMetrics> Covered() =>
    [
        WithCoverage("a.Critical", 8, 5.0, 0.25),
        WithCoverage("a.High", 5, 5.0, 0.6),
        WithCoverage("a.Medium", 3, 5.0, 0.75),
        WithCoverage("a.Low", 2, 5.0, 1.0),
        WithCoverage("a.Unknown", 1, 0.5, 0.0),
        new("a.NotInReport", "a.NotInReport.cs", 1, 1, 1),
    ];

    private static ChurnMetrics WithCoverage(string name, int timesChanged, double score, double coverage)
    {
        double churn = GitChurnCalculator.Churn(timesChanged, score);
        return new ChurnMetrics(
            name, name + ".cs", score, timesChanged, churn, coverage,
            GitChurnCalculator.RiskChurn(timesChanged, score, coverage),
            GitChurnCalculator.RiskLevel(churn, coverage));
    }

    private static List<ChurnMetrics> Awkward() =>
    [
        new("a.B<T>|\"x\"&'y'", "f<1>.cs", 1, 2, 2, 0.5, 1, ChurnRiskLevel.Low),
        new("a.NoChurn", "g.cs", 0, 0, 0),
        new("a.NoScore", "h.cs", 0, 4, 0),
    ];

    [TestCase(ChurnReportType.Json, "json", "sample.json")]
    [TestCase(ChurnReportType.Csv, "csv", "sample.csv")]
    [TestCase(ChurnReportType.Html, "html", "sample.html")]
    [TestCase(ChurnReportType.Markdown, "markdown", "sample.md")]
    public void Render_WithoutCoverageMatchesTheGoldenFile(ChurnReportType type, string _, string golden)
    {
        ChurnGolden.AssertEqual(golden, _builder.Render(type, Sample()));
    }

    [TestCase(ChurnReportType.Json, "covered.json")]
    [TestCase(ChurnReportType.Csv, "covered.csv")]
    [TestCase(ChurnReportType.Html, "covered.html")]
    [TestCase(ChurnReportType.Markdown, "covered.md")]
    public void Render_WithCoverageMatchesTheGoldenFile(ChurnReportType type, string golden)
    {
        ChurnGolden.AssertEqual(golden, _builder.Render(type, Covered()));
    }

    [TestCase(ChurnReportType.Json, "awkward.json")]
    [TestCase(ChurnReportType.Csv, "awkward.csv")]
    [TestCase(ChurnReportType.Html, "awkward.html")]
    [TestCase(ChurnReportType.Markdown, "awkward.md")]
    [TestCase(ChurnReportType.SvgTreemap, "awkward.svg")]
    public void Render_EscapesAwkwardNamesAndKeepsClassesWithoutChurnWhereTheFormatShowsThem(
        ChurnReportType type, string golden)
    {
        ChurnGolden.AssertEqual(golden, _builder.Render(type, Awkward()));
    }

    [Test]
    public void Render_SvgTreemapIsByteIdenticalToTheReferenceImplementation()
    {
        string svg = _builder.Render(ChurnReportType.SvgTreemap, Sample());

        Assert.That(svg, Is.EqualTo(ChurnGolden.Read("treemap.php-reference.svg")));
    }

    [Test]
    public void Render_SvgTreemapOfNothingIsAnEmptyCanvas()
    {
        string svg = _builder.Render(ChurnReportType.SvgTreemap, Awkward().Skip(1).ToList());

        Assert.That(svg, Does.Contain("Churn Treemap</text>"));
        Assert.That(svg, Does.Not.Contain("<title>"));
    }

    [Test]
    public void Render_SvgTreemapEscapesTextAndTruncatesLongLabelsToFortyCharactersWithAnEllipsis()
    {
        string longName = new string('a', 39) + "bcd<";

        string svg = _builder.Render(ChurnReportType.SvgTreemap,
        [
            new ChurnMetrics(longName, "f", 1, 1, 1),
            new ChurnMetrics("<&>", "g", 1, 1, 1),
        ]);

        Assert.That(svg, Does.Contain("<title>" + new string('a', 39) + "bcd&lt;&#10;"));
        Assert.That(svg, Does.Contain(">" + new string('a', 39) + "…</text>"));
        Assert.That(svg, Does.Contain("<title>&lt;&amp;&gt;&#10;"));
        Assert.That(svg, Does.Contain(">&lt;&amp;&gt;</text>"));
    }

    [Test]
    public void Render_SvgTreemapKeepsALabelOfExactlyFortyCharactersWhole()
    {
        string name = new string('a', 40);

        string svg = _builder.Render(ChurnReportType.SvgTreemap, [new ChurnMetrics(name, "f", 1, 1, 1)]);

        Assert.That(svg, Does.Contain(">" + name + "</text>"));
    }

    [Test]
    public void Render_SvgTreemapCountsSurrogatePairsAsOneCharacter()
    {
        string name = string.Concat(Enumerable.Repeat("😀", 40));

        string svg = _builder.Render(ChurnReportType.SvgTreemap, [new ChurnMetrics(name, "f", 1, 1, 1)]);

        Assert.That(svg, Does.Contain(">" + name + "</text>"));
        Assert.That(
            _builder.Render(ChurnReportType.SvgTreemap, [new ChurnMetrics(name + "x", "f", 1, 1, 1)]),
            Does.Contain(">" + string.Concat(Enumerable.Repeat("😀", 39)) + "…</text>"));
    }

    [Test]
    public void Render_WithoutCoverageHasNoCoverageColumns()
    {
        foreach (ChurnReportType type in new[] { ChurnReportType.Csv, ChurnReportType.Html, ChurnReportType.Markdown })
        {
            Assert.That(_builder.Render(type, Sample()), Does.Not.Contain("Coverage").And.Not.Contain("Risk"), type.ToString());
        }
    }

    [Test]
    public void Render_WithCoverageProvidedShowsTheCoverageColumnsEvenWhenNoClassHasCoverage()
    {
        var builder = new ChurnReportBuilder(FixedTimeProvider.Default, coverageProvided: true);

        Assert.That(builder.Render(ChurnReportType.Csv, Sample()), Does.StartWith(
            "Class,File,Score,Churn,Times Changed,Risk Churn,Coverage,Risk Level\n"));
        Assert.That(builder.Render(ChurnReportType.Markdown, Sample()), Does.Contain("| N/A | 6 | N/A | N/A |"));
        Assert.That(builder.Render(ChurnReportType.Html, Sample()), Does.Contain("<td>N/A</td>"));
    }

    [Test]
    public void Render_JsonHasNullsForClassesWithoutCoverage()
    {
        string json = _builder.Render(ChurnReportType.Json, Covered());

        Assert.That(json, Does.Contain("\"riskLevel\": \"CRITICAL\"").And.Contain("\"riskLevel\": \"HIGH\"")
            .And.Contain("\"riskLevel\": \"MEDIUM\"").And.Contain("\"riskLevel\": \"LOW\""));
        Assert.That(json, Does.Contain("\"coverage\": null,\n      \"riskChurn\": null,\n      \"riskLevel\": null"));
        Assert.That(json, Does.Contain("\"createdAt\": \"2023-10-01 12:00:00\""));
    }

    [Test]
    public void Render_CoverageIsNotRoundedInCsvAndJson()
    {
        var metrics = new List<ChurnMetrics>
        {
            new("a.A", "a.cs", 1, 1, 1, 1 / 3.0, 0.667, ChurnRiskLevel.Low),
        };

        Assert.That(_builder.Render(ChurnReportType.Csv, metrics), Does.Contain(",0.667,0.3333333333333333,LOW\n"));
        Assert.That(_builder.Render(ChurnReportType.Json, metrics), Does.Contain("\"coverage\": 0.3333333333333333"));
        Assert.That(_builder.Render(ChurnReportType.Markdown, metrics), Does.Contain("| 33.33% | LOW |"));
    }

    [Test]
    public void Render_IsIndependentOfTheCurrentCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        CultureInfo german;
        try
        {
            german = new CultureInfo("de-DE");
        }
        catch (CultureNotFoundException)
        {
            Assert.Ignore("de-DE is not available (invariant globalization).");
            return;
        }

        CultureInfo.CurrentCulture = german;
        try
        {
            Assert.That(_builder.Render(ChurnReportType.SvgTreemap, Sample()), Does.Contain("x=\"2.00\"").And.Contain("Churn: 12.252&#10;"));
            Assert.That(_builder.Render(ChurnReportType.Html, Sample()), Does.Contain("<td>12.252</td>").And.Contain("<td>8.060</td>"));
            Assert.That(_builder.Render(ChurnReportType.Csv, Sample()), Does.Contain(",2.042,12.252,6\n"));
            Assert.That(_builder.Render(ChurnReportType.Json, Sample()), Does.Contain("\"score\": 2.042"));
            Assert.That(_builder.Render(ChurnReportType.Markdown, Covered()), Does.Contain("25.00%"));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Test]
    public void Render_TimestampIsLocalTimeInTheDocumentedFormat()
    {
        var provider = new ZonedTimeProvider(
            new DateTimeOffset(2024, 3, 5, 23, 4, 9, TimeSpan.Zero), TimeZoneInfo.CreateCustomTimeZone("t", TimeSpan.FromHours(2), "t", "t"));

        string markdown = new ChurnReportBuilder(provider).Render(ChurnReportType.Markdown, []);

        Assert.That(markdown, Does.Contain("Generated: 2024-03-06 01:04:09\n"));
    }

    private sealed class ZonedTimeProvider(DateTimeOffset utc, TimeZoneInfo zone) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utc;

        public override TimeZoneInfo LocalTimeZone => zone;
    }

    [Test]
    public void Render_CsvQuotesFieldsThatStartWithASeparatorOrContainLineBreaks()
    {
        string csv = _builder.Render(ChurnReportType.Csv,
        [
            new ChurnMetrics(",x", "a\nb", 1, 1, 1),
            new ChurnMetrics("y\rz", "f", 1, 1, 1),
        ]);

        Assert.That(csv, Does.Contain("\",x\",\"a\nb\",1,1,1\n"));
        Assert.That(csv, Does.Contain("\"y\rz\",f,1,1,1\n"));
    }

    [TestCase(1, 0)]
    [TestCase(0, 1)]
    public void Render_MarkdownSkipsClassesWithoutScoreOrWithoutChurn(double score, double churn)
    {
        string markdown = _builder.Render(ChurnReportType.Markdown, [new ChurnMetrics("a.Skipped", "f", score, 1, churn)]);

        Assert.That(markdown, Does.Not.Contain("a.Skipped"));
    }

    [Test]
    public void Render_UnknownTypeIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _builder.Render((ChurnReportType)99, Sample()));
    }

    [Test]
    public void Write_WritesUtf8WithoutBomAndCreatesMissingDirectories()
    {
        var temp = new TempFiles();
        try
        {
            string file = Path.Combine(temp.tmpDirectory, "sub", "dir", "report.md");

            _builder.Write(ChurnReportType.Markdown, file, [new ChurnMetrics("a.Ünïcode", "f", 1, 1, 1)]);

            byte[] bytes = File.ReadAllBytes(file);
            Assert.That(bytes[0], Is.Not.EqualTo(0xEF), "no BOM");
            Assert.That(System.Text.Encoding.UTF8.GetString(bytes), Does.Contain("| a.Ünïcode | 1 | 1 | 1 |"));
        }
        finally
        {
            temp.CleanUp();
        }
    }
}
