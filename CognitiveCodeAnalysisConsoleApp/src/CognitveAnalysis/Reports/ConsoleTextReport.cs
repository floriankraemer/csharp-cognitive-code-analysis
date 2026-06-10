/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.CognitiveAnalysis.Reports;

public class ConsoleTextReport() : IReport
{
    public string Name => "ConsoleText";

    public void RenderMetrics(
        string outputFile,
        CognitiveMetricsCollection metricsCollection,
        CognitiveConfiguration configuration,
        CognitiveBaselineComparison? baselineComparison = null
    )
    {
        CognitiveMetricsCollection filteredCollection = ReportMetricsFilter.FilterForReport(metricsCollection, configuration);

        bool hasCoverageData = filteredCollection.HasCoverageData();

        if (configuration.GroupByClass)
        {
            RenderMetricsGrouped(filteredCollection, hasCoverageData, configuration, metricsCollection, baselineComparison);
            RenderSummary(metricsCollection, configuration);
            return;
        }

        foreach (CognitiveMetrics metrics in filteredCollection)
        {
            RenderMetrics(metrics, hasCoverageData, configuration, baselineComparison);
        }

        RenderSummary(metricsCollection, configuration);
    }

    private static void RenderMetricsGrouped(
        CognitiveMetricsCollection metricsCollection,
        bool hasCoverageData,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection fullMetricsCollection,
        CognitiveBaselineComparison? baselineComparison
    ) {
        var groupedByClass = metricsCollection
            .GroupBy(metrics => new { metrics.ClassName, metrics.FilePath })
            .OrderBy(g => g.Key.ClassName);

        foreach (var classGroup in groupedByClass)
        {
            List<CognitiveMetrics> classMetrics = classGroup.ToList();

            // Skip classes with no metrics (after filtering)
            if (classMetrics.Count == 0)
            {
                continue;
            }

            CognitiveMetrics firstMetric = classMetrics.First();

            AnsiConsole.MarkupLine($"[blue]Class:[/] {Markup.Escape(firstMetric.ClassName)}");
            AnsiConsole.MarkupLine($"[yellow]File:[/] {Markup.Escape(firstMetric.FilePath)}");
            RenderCouplingLine(configuration, fullMetricsCollection, firstMetric.ClassName, baselineComparison);

            Table table = new();
            table = AddTableHeaders(table, hasCoverageData, configuration);
            table.ShowRowSeparators();

            table = classMetrics.Aggregate(
                table,
                (current, metrics) => AddTableRow(current, metrics, hasCoverageData, configuration, baselineComparison)
            );

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    private static void RenderMetrics(
        CognitiveMetrics metrics,
        bool hasCoverageData,
        CognitiveConfiguration configuration,
        CognitiveBaselineComparison? baselineComparison
    )
    {
        RenderMetricsSummary(metrics);

        Table table = new();
        table = AddTableHeaders(table, hasCoverageData, configuration);
        table = AddTableRow(table, metrics, hasCoverageData, configuration, baselineComparison);

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static Table AddTableRow(
        Table table,
        CognitiveMetrics metrics,
        bool hasCoverageData,
        CognitiveConfiguration configuration,
        CognitiveBaselineComparison? baselineComparison
    ) {
        MethodMetricsComparison? comparison = null;
        baselineComparison?.TryGetMethodComparison(metrics, out comparison);

        var rowData = new List<string>
        {
            "L" + metrics.methodLineNumber + " " + Markup.Escape(metrics.MethodName),
            CognitiveReportDeltaFormatter.FormatConsoleValue(ColorizeScore(metrics.totalScore), comparison?.TotalScore, "F3"),
            FormatCountWithScore(metrics.linesOfCode, metrics.linesOfCodeScore, comparison?.LinesOfCode, comparison?.LinesOfCodeScore),
            FormatCountWithScore(metrics.ifCount, metrics.ifScore, comparison?.IfCount, comparison?.IfScore),
            FormatCountWithScore(metrics.argumentCount, metrics.argumentScore, comparison?.ArgumentCount, comparison?.ArgumentScore),
            FormatCountWithScore(metrics.nestingLevels, metrics.nestingScore, comparison?.NestingLevels, comparison?.NestingScore),
            FormatCountWithScore(metrics.returnCount, metrics.returnScore, comparison?.ReturnCount, comparison?.ReturnScore),
            FormatCountWithScore(metrics.localVariableCount, metrics.localVariableScore, comparison?.LocalVariableCount, comparison?.LocalVariableScore),
            FormatCountWithScore(metrics.fieldAccessCount, metrics.fieldAccessScore, comparison?.FieldAccessCount, comparison?.FieldAccessScore),
            FormatCountWithScore(metrics.propertyAccessCount, metrics.propertyAccessScore, comparison?.PropertyAccessCount, comparison?.PropertyAccessScore),
        };

        if (configuration.ShowHalsteadComplexity)
        {
            rowData.Add(FormatHalsteadVolume(metrics, comparison));
            rowData.Add(FormatHalsteadDifficulty(metrics, comparison));
            rowData.Add(FormatHalsteadEffort(metrics, comparison));
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            rowData.Add(FormatScoreWithDelta(metrics.cyclomaticComplexity, comparison?.CyclomaticComplexity, "F1"));
        }

        if (hasCoverageData)
        {
            rowData.Add(FormatCoverage(metrics, comparison));
            rowData.Add(FormatChurnScore(metrics, comparison));
        }

        table.AddRow(rowData.ToArray());

        return table;
    }

    /// <summary>
    /// Formats coverage percentages for display.
    /// Shows both line and branch coverage if available, or "n/a" if no coverage data.
    /// </summary>
    /// <param name="metrics">The cognitive metrics with coverage data</param>
    /// <returns>Formatted string with coverage percentages, or "n/a" if no coverage</returns>
    private static string FormatCountWithScore(
        int count,
        double score,
        MetricDelta? countDelta,
        MetricDelta? scoreDelta
    )
    {
        string countPart = CognitiveReportDeltaFormatter.FormatConsoleValue(
            count.ToString(CultureInfo.InvariantCulture),
            countDelta,
            "F0");
        string scorePart = CognitiveReportDeltaFormatter.FormatConsoleValue(
            ColorizeScore(score),
            scoreDelta,
            "F3");
        return countPart + " (" + scorePart + ")";
    }

    private static string FormatScoreWithDelta(double score, MetricDelta? delta, string format = "F3") =>
        CognitiveReportDeltaFormatter.FormatConsoleValue(
            score.ToString(format, CultureInfo.InvariantCulture),
            delta,
            format);

    private static string FormatCoverage(CognitiveMetrics metrics, MethodMetricsComparison? comparison)
    {
        bool hasLineCoverage = metrics.lineCoveragePercentage.HasValue;
        bool hasBranchCoverage = metrics.branchCoveragePercentage.HasValue;

        if (!hasLineCoverage && !hasBranchCoverage)
        {
            return "[dim]n/a[/]";
        }

        var parts = new List<string>();

        if (hasLineCoverage)
        {
            double lineCoverage = metrics.lineCoveragePercentage!.Value;
            string lineColor = GetCoverageColor(lineCoverage);
            string lineText = $"[{lineColor}]Line: {lineCoverage:F1}%[/]";
            parts.Add(CognitiveReportDeltaFormatter.FormatConsoleValue(lineText, comparison?.LineCoveragePercentage, "F1"));
        }

        if (hasBranchCoverage)
        {
            double branchCoverage = metrics.branchCoveragePercentage!.Value;
            string branchColor = GetCoverageColor(branchCoverage);
            string branchText = $"[{branchColor}]Branch: {branchCoverage:F1}%[/]";
            parts.Add(CognitiveReportDeltaFormatter.FormatConsoleValue(branchText, comparison?.BranchCoveragePercentage, "F1"));
        }

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// <![CDATA[
    /// Gets the color for coverage percentage based on thresholds.
    /// Green >= 80%, Yellow >= 50%, Red < 50%
    /// ]]>
    /// </summary>
    /// <param name="coverage">Coverage percentage (0-100)</param>
    /// <returns>Color name for markup</returns>
    private static string GetCoverageColor(double coverage)
    {
        return coverage switch
        {
            >= 80.0 => "green",
            >= 50.0 => "yellow",
            _ => "red",
        };
    }

    /// <summary>
    /// <![CDATA[
    /// Formats the churn score for display with color coding.
    /// Green < 0.3 (low risk), Yellow 0.3-0.7 (medium risk), Red > 0.7 (high risk)
    /// ]]>
    /// </summary>
    /// <param name="metrics">The cognitive metrics with churn score</param>
    /// <returns>Formatted string with churn score, or "n/a" if no churn score</returns>
    private static string FormatChurnScore(CognitiveMetrics metrics, MethodMetricsComparison? comparison)
    {
        if (!metrics.churnScore.HasValue)
        {
            return "[dim]n/a[/]";
        }

        double churnScore = metrics.churnScore.Value;
        string color = GetChurnColor(churnScore);
        string churnText = $"[{color}]{churnScore:F3}[/]";
        return CognitiveReportDeltaFormatter.FormatConsoleValue(churnText, comparison?.ChurnScore, "F3");
    }

    /// <summary>
    /// <![CDATA[
    /// Gets the color for churn score based on risk thresholds.
    /// Green < 0.3 (low risk), Yellow 0.3-0.7 (medium risk), Red > 0.7 (high risk)
    /// ]]>
    /// </summary>
    /// <param name="churnScore">The churn score</param>
    /// <returns>Color name for markup</returns>
    private static string GetChurnColor(double churnScore)
    {
        if (churnScore < 0.3)
        {
            return "green";
        }
        else if (churnScore <= 0.7)
        {
            return "yellow";
        }
        else
        {
            return "red";
        }
    }

    /// <summary>
    /// <![CDATA[
    /// Colorizes the metric score based on thresholds.
    /// ]]>>
    /// </summary>
    /// <param name="score"></param>
    /// <returns></returns>
    private static string ColorizeScore(double score)
    {
        (double, string)[] colorMap =
        [
            (0.5, "green"),
            (0.85, "yellow"),
            (double.MaxValue, "red")
        ];

        string color = colorMap.First(x => score < x.Item1).Item2;

        return $"[{color}]{score:F3}[/]";
    }

    private static void RenderCouplingLine(
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection metricsCollection,
        string className,
        CognitiveBaselineComparison? baselineComparison
    ) {
        if (!configuration.GroupByClass || !configuration.ShowCouplingMetrics)
        {
            return;
        }

        string couplingText = FormatCouplingMetrics(metricsCollection, className, baselineComparison);
        AnsiConsole.MarkupLine($"[cyan]Coupling:[/] {couplingText}");
    }

    private static string FormatCouplingMetrics(
        CognitiveMetricsCollection metricsCollection,
        string className,
        CognitiveBaselineComparison? baselineComparison
    )
    {
        if (!metricsCollection.TryGetClassCoupling(className, out var coupling) || coupling == null)
        {
            return Markup.Escape("n/a");
        }

        ClassCouplingComparison? couplingComparison = null;
        baselineComparison?.TryGetClassCouplingComparison(className, out couplingComparison);

        string incoming = coupling.IncomingCoupling.ToString(CultureInfo.InvariantCulture);
        string outgoing = coupling.OutgoingCoupling.ToString(CultureInfo.InvariantCulture);
        string stability = coupling.Stability.ToString("F3", CultureInfo.InvariantCulture);

        if (couplingComparison is { HasBaseline: true })
        {
            incoming = CognitiveReportDeltaFormatter.FormatConsoleValue(incoming, couplingComparison.IncomingCoupling, "F0");
            outgoing = CognitiveReportDeltaFormatter.FormatConsoleValue(outgoing, couplingComparison.OutgoingCoupling, "F0");
            stability = CognitiveReportDeltaFormatter.FormatConsoleValue(stability, couplingComparison.Stability, "F3");
        }

        return $"In={incoming}, Out={outgoing}, Stability={stability}";
    }

    private static void RenderMetricsSummary(CognitiveMetrics metrics)
    {
        AnsiConsole.MarkupLine($"[blue]Class:[/] {Markup.Escape(metrics.ClassName)}");
        AnsiConsole.MarkupLine($"[green]Method:[/] {Markup.Escape(metrics.methodSignature)}");
        AnsiConsole.MarkupLine($"[yellow]File:[/] {Markup.Escape(metrics.FilePath)}");

        AnsiConsole.WriteLine();
    }

    private static string FormatHalsteadVolume(CognitiveMetrics metrics, MethodMetricsComparison? comparison)
        => FormatHalsteadValue(metrics.Halstead?.Volume, comparison?.HalsteadVolume);

    private static string FormatHalsteadDifficulty(CognitiveMetrics metrics, MethodMetricsComparison? comparison)
        => FormatHalsteadValue(metrics.Halstead?.Difficulty, comparison?.HalsteadDifficulty);

    private static string FormatHalsteadEffort(CognitiveMetrics metrics, MethodMetricsComparison? comparison)
        => FormatHalsteadValue(metrics.Halstead?.Effort, comparison?.HalsteadEffort);

    private static string FormatHalsteadValue(double? value, MetricDelta? delta)
    {
        if (!value.HasValue)
        {
            return "[dim]n/a[/]";
        }

        return CognitiveReportDeltaFormatter.FormatConsoleValue(
            value.Value.ToString("F2", CultureInfo.InvariantCulture),
            delta,
            "F2");
    }

    private static Table AddTableHeaders(Table table, bool hasCoverageData, CognitiveConfiguration configuration)
    {
        table.AddColumn("Method");
        table.AddColumn("Score");
        table.AddColumn("Lines");
        table.AddColumn("Ifs");
        table.AddColumn("Arguments");
        table.AddColumn("Nesting");
        table.AddColumn("Returns");
        table.AddColumn("Locals");
        table.AddColumn("Fields");
        table.AddColumn("Props");

        if (configuration.ShowHalsteadComplexity)
        {
            table.AddColumn("Halstead Vol");
            table.AddColumn("Halstead Diff");
            table.AddColumn("Halstead Effort");
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            table.AddColumn("Cyclomatic");
        }

        if (!hasCoverageData)
        {
            return table;
        }

        table.AddColumn("Coverage");
        table.AddColumn("Churn");

        return table;
    }

    private void RenderSummary(CognitiveMetricsCollection metricsCollection, CognitiveConfiguration configuration)
    {
        if (metricsCollection.Count == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold cyan]Summary:[/]");
        double scoreThreshold = configuration.ScoreThreshold;

        AnsiConsole.MarkupLine(
            $"[blue]Total Classes Processed:[/] "
            + $"{metricsCollection.GetTotalClasses()}"
        );

        AnsiConsole.MarkupLine(
            $"[blue]Total Methods Processed:[/] "
            + $"{metricsCollection.GetTotalMethods()}"
        );

        AnsiConsole.MarkupLine(
            $"[yellow]Classes with Methods Exceeding Threshold:[/] "
            + $"{metricsCollection.GetClassesWithExceedingMethods(scoreThreshold)} "
            + $"({metricsCollection.GetClassesPercentage(scoreThreshold):F1}%)"
        );

        AnsiConsole.MarkupLine(
            $"[yellow]Methods Exceeding Threshold:[/] "
            + $"{metricsCollection.GetMethodsExceedingThreshold(scoreThreshold)} "
            + $"({metricsCollection.GetMethodsPercentage(scoreThreshold):F1}%)"
        );

        AnsiConsole.MarkupLine($"Threshold: {scoreThreshold:F3}");
    }
}
