/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.CognitiveAnalysis.Reports;

public class ConsoleTextReport() : IReport
{
    public string Name => "ConsoleText";

    public void RenderMetrics(string outputFile, CognitiveMetricsCollection metricsCollection, CognitiveConfiguration configuration)
    {
        CognitiveMetricsCollection filteredCollection = FilterMetrics(metricsCollection, configuration);

        bool hasCoverageData = filteredCollection.HasCoverageData();

        if (configuration.GroupByClass)
        {
            RenderMetricsGrouped(filteredCollection, hasCoverageData);
            RenderSummary(metricsCollection, configuration);
            return;
        }

        foreach (CognitiveMetrics metrics in filteredCollection)
        {
            RenderMetrics(metrics, hasCoverageData);
        }

        RenderSummary(metricsCollection, configuration);
    }

    private static void RenderMetricsGrouped(CognitiveMetricsCollection metricsCollection, bool hasCoverageData)
    {
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

            Table table = new();
            table = AddTableHeaders(table, hasCoverageData);
            table.ShowRowSeparators();

            table = classMetrics.Aggregate(table, (current, metrics) => AddTableRow(current, metrics, hasCoverageData));

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    /// <summary>
    /// <![CDATA[
    /// Filters the metrics collection based on ShowOnlyMethodsExceedingThreshold configuration.
    /// ]]>
    /// </summary>
    /// <param name="metricsCollection">The original metrics collection</param>
    /// <returns>Filtered metrics collection</returns>
    private CognitiveMetricsCollection FilterMetrics(
        CognitiveMetricsCollection metricsCollection,
        CognitiveConfiguration configuration
    ) {
        if (!configuration.ShowOnlyMethodsExceedingThreshold)
        {
            return metricsCollection;
        }

        return metricsCollection.OnlyMetricsExceedingScoreThreshold(configuration.ScoreThreshold);
    }

    private static void RenderMetrics(CognitiveMetrics metrics, bool hasCoverageData)
    {
        RenderMetricsSummary(metrics);

        Table table = new();
        table = AddTableHeaders(table, hasCoverageData);
        table = AddTableRow(table, metrics, hasCoverageData);

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static Table AddTableRow(
        Table table,
        CognitiveMetrics metrics,
        bool hasCoverageData
    ) {
        var rowData = new List<string>
        {
            "L" + metrics.methodLineNumber + " " + Markup.Escape(metrics.MethodName),
            ColorizeScore(metrics.totalScore),
            metrics.linesOfCode.ToString(),
            metrics.ifCount + " (" + ColorizeScore(metrics.ifScore) + ")",
            metrics.argumentCount + " (" + ColorizeScore(metrics.argumentScore) + ")",
            metrics.nestingLevels + " (" + ColorizeScore(metrics.nestingScore) + ")",
            metrics.returnCount + " (" + ColorizeScore(metrics.returnScore) + ")",
            metrics.localVariableCount + " (" + ColorizeScore(metrics.localVariableScore) + ")",
            metrics.fieldAccessCount + " (" + ColorizeScore(metrics.fieldAccessScore) + ")",
            metrics.propertyAccessCount + " (" + ColorizeScore(metrics.propertyAccessScore) + ")",
        };

        if (hasCoverageData)
        {
            rowData.Add(FormatCoverage(metrics));
            rowData.Add(FormatChurnScore(metrics));
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
    private static string FormatCoverage(CognitiveMetrics metrics)
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
            parts.Add($"[{lineColor}]Line: {lineCoverage:F1}%[/]");
        }

        if (hasBranchCoverage)
        {
            double branchCoverage = metrics.branchCoveragePercentage!.Value;
            string branchColor = GetCoverageColor(branchCoverage);
            parts.Add($"[{branchColor}]Branch: {branchCoverage:F1}%[/]");
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
    private static string FormatChurnScore(CognitiveMetrics metrics)
    {
        if (!metrics.churnScore.HasValue)
        {
            return "[dim]n/a[/]";
        }

        double churnScore = metrics.churnScore.Value;
        string color = GetChurnColor(churnScore);

        return $"[{color}]{churnScore:F3}[/]";
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

    private static void RenderMetricsSummary(CognitiveMetrics metrics)
    {
        AnsiConsole.MarkupLine($"[blue]Class:[/] {Markup.Escape(metrics.ClassName)}");
        AnsiConsole.MarkupLine($"[green]Method:[/] {Markup.Escape(metrics.methodSignature)}");
        AnsiConsole.MarkupLine($"[yellow]File:[/] {Markup.Escape(metrics.FilePath)}");

        AnsiConsole.WriteLine();
    }

    private static Table AddTableHeaders(Table table, bool hasCoverageData)
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
