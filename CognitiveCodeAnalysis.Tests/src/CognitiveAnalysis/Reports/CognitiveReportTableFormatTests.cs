/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.HalsteadAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class CognitiveReportTableFormatTests
{
    private const int BaseColumns = 14;
    private const int NonDetailColumns = 2;

    private static CognitiveMetrics Metric() => new("Run", "Svc", "a.cs", "void Run()", 12, ifCount: 2, linesOfCode: 7)
    {
        ifScore = 0.5,
        totalScore = 1.23456,
        cyclomaticComplexity = 3.26,
    };

    [Test]
    public void BuildColumnHeaders_Default_HasOnlyBaseColumns()
    {
        var headers = CognitiveReportTableFormat.BuildColumnHeaders(new CognitiveConfiguration(), hasCoverageData: false);

        Assert.That(headers, Has.Count.EqualTo(BaseColumns));
        Assert.That(headers[0], Is.EqualTo("Method"));
        Assert.That(headers[^1], Is.EqualTo("Props"));
    }

    [Test]
    public void BuildColumnHeaders_DetailColumns_IncludeElseLoopsSwitchTryCatch()
    {
        var headers = CognitiveReportTableFormat.BuildColumnHeaders(new CognitiveConfiguration(), hasCoverageData: false);

        Assert.That(headers, Is.EqualTo(new[]
        {
            "Method", "Score", "Lines", "Ifs", "Else", "Loops", "Switch", "Try/Catch",
            "Arguments", "Nesting", "Returns", "Locals", "Fields", "Props",
        }));
    }

    [Test]
    public void BuildColumnHeaders_DetailedMetricsOff_KeepsMethodScoreAndOptionalExtras()
    {
        var config = new CognitiveConfiguration
        {
            ShowDetailedCognitiveMetrics = false,
            ShowHalsteadComplexity = true,
            ShowCyclomaticComplexity = true,
        };

        var headers = CognitiveReportTableFormat.BuildColumnHeaders(config, hasCoverageData: true);

        Assert.That(headers, Is.EqualTo(new[]
        {
            "Method", "Score", "Halstead Volume", "Halstead Difficulty", "Halstead Effort", "Cyclomatic Complexity", "Churn",
        }));
    }

    [Test]
    public void BuildRowValues_DetailedMetricsOff_MatchesHeaderCount()
    {
        var config = new CognitiveConfiguration { ShowDetailedCognitiveMetrics = false };

        var row = CognitiveReportTableFormat.BuildRowValues(Metric(), config, hasCoverageData: false);

        Assert.That(row, Is.EqualTo(new[] { "L12 Run", "1.235" }));
        Assert.That(row, Has.Count.EqualTo(NonDetailColumns));
    }

    [Test]
    public void BuildColumnHeaders_AllOptions_AppendsHalsteadCyclomaticChurnInOrder()
    {
        var config = new CognitiveConfiguration { ShowHalsteadComplexity = true, ShowCyclomaticComplexity = true };

        var headers = CognitiveReportTableFormat.BuildColumnHeaders(config, hasCoverageData: true);

        Assert.That(headers.Skip(BaseColumns), Is.EqualTo(new[]
        {
            "Halstead Volume", "Halstead Difficulty", "Halstead Effort", "Cyclomatic Complexity", "Churn",
        }));
    }

    [Test]
    public void BuildColumnHeaders_OnlyHalstead_DoesNotAddCyclomaticOrChurn()
    {
        var headers = CognitiveReportTableFormat.BuildColumnHeaders(
            new CognitiveConfiguration { ShowHalsteadComplexity = true }, hasCoverageData: false);

        Assert.That(headers, Has.Count.EqualTo(BaseColumns + 3));
    }

    [Test]
    public void BuildColumnHeaders_OnlyCyclomatic_AddsOneColumn()
    {
        var headers = CognitiveReportTableFormat.BuildColumnHeaders(
            new CognitiveConfiguration { ShowCyclomaticComplexity = true }, hasCoverageData: false);

        Assert.That(headers, Has.Count.EqualTo(BaseColumns + 1));
    }

    [Test]
    public void BuildRowValues_Default_MatchesHeaderCountAndFormatsValues()
    {
        var row = CognitiveReportTableFormat.BuildRowValues(Metric(), new CognitiveConfiguration(), hasCoverageData: false);

        Assert.That(row, Has.Count.EqualTo(BaseColumns));
        Assert.That(row[0], Is.EqualTo("L12 Run"));
        Assert.That(row[1], Is.EqualTo("1.235"));
        Assert.That(row[2], Is.EqualTo("7"));
        Assert.That(row[3], Is.EqualTo("2 (0.500)"));
    }

    [Test]
    public void BuildRowValues_AllOptionsWithData_AppendsFormattedExtras()
    {
        var m = Metric();
        m.Halstead = new HalsteadMetrics { Volume = 1.005, Difficulty = 2, Effort = 3.126 };
        m.churnScore = 4.5;
        var config = new CognitiveConfiguration { ShowHalsteadComplexity = true, ShowCyclomaticComplexity = true };

        var row = CognitiveReportTableFormat.BuildRowValues(m, config, hasCoverageData: true);

        Assert.That(row.Skip(BaseColumns), Is.EqualTo(new[] { "1.00", "2.00", "3.13", "3.3", "4.500" }));
    }

    [Test]
    public void BuildRowValues_MissingHalsteadAndChurn_UsesNotAvailableMarker()
    {
        var config = new CognitiveConfiguration { ShowHalsteadComplexity = true };

        var row = CognitiveReportTableFormat.BuildRowValues(Metric(), config, hasCoverageData: true);

        Assert.That(row.Skip(BaseColumns), Is.EqualTo(new[] { "n/a", "n/a", "n/a", "n/a" }));
    }

    [Test]
    public void BuildRowValues_CoverageDisabled_OmitsChurnEvenWhenSet()
    {
        var m = Metric();
        m.churnScore = 4.5;

        var row = CognitiveReportTableFormat.BuildRowValues(m, new CognitiveConfiguration(), hasCoverageData: false);

        Assert.That(row, Has.Count.EqualTo(BaseColumns));
    }

    [Test]
    public void FormatInvariant_IgnoresCurrentCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            Assert.That(CognitiveReportTableFormat.FormatInvariant(1.5, "F3"), Is.EqualTo("1.500"));
            Assert.That(CognitiveReportTableFormat.FormatHalsteadDouble(1.5), Is.EqualTo("1.50"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    public void EscapeMarkdownTableCell_EscapesPipesOnly()
    {
        Assert.That(CognitiveReportTableFormat.EscapeMarkdownTableCell("a|b|c"), Is.EqualTo("a\\|b\\|c"));
        Assert.That(CognitiveReportTableFormat.EscapeMarkdownTableCell("plain"), Is.EqualTo("plain"));
    }
}
