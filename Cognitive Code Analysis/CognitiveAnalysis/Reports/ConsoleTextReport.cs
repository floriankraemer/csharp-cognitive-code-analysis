using CognitiveCodeAnalysis.Configuration;
using Spectre.Console;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public class ConsoleTextReport(CognitiveConfiguration configuration) : ReportInterface
{
    public void RenderMetrics(CognitiveMetricsCollection metricsCollection)
    {
        // Filter metrics if ShowOnlyMethodsExceedingThreshold is enabled
        CognitiveMetricsCollection filteredCollection = FilterMetrics(metricsCollection);

        // Check if any metrics have coverage data (check original collection, not filtered)
        bool hasCoverageData = metricsCollection.Any(m =>
            m.LineCoveragePercentage.HasValue || m.BranchCoveragePercentage.HasValue);

        if (configuration.GroupByClass)
        {
            RenderMetricsGrouped(filteredCollection, hasCoverageData);
            RenderSummary(metricsCollection);
            return;
        }

        foreach (CognitiveMetrics metrics in filteredCollection)
        {
            RenderMetrics(metrics, hasCoverageData);
        }

        RenderSummary(metricsCollection);
    }

    private void RenderMetricsGrouped(CognitiveMetricsCollection metricsCollection, bool hasCoverageData)
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

            foreach (CognitiveMetrics metrics in classMetrics)
            {
                table = AddTableRow(table, metrics, hasCoverageData);
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    /// <summary>
    /// Filters the metrics collection based on ShowOnlyMethodsExceedingThreshold configuration.
    /// </summary>
    /// <param name="metricsCollection">The original metrics collection</param>
    /// <returns>Filtered metrics collection</returns>
    private CognitiveMetricsCollection FilterMetrics(CognitiveMetricsCollection metricsCollection)
    {
        if (!configuration.ShowOnlyMethodsExceedingThreshold)
        {
            return metricsCollection;
        }

        double scoreThreshold = configuration.ScoreThreshold;
        CognitiveMetricsCollection filtered = new();

        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            if (metrics.TotalScore > scoreThreshold)
            {
                filtered.Add(metrics);
            }
        }

        return filtered;
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
            ColorizeScore(metrics.TotalScore),
            metrics.linesOfCode.ToString(),
            metrics.ifCount + " (" + ColorizeScore(metrics.ifScore) + ")",
            metrics.argumentCount + " (" + ColorizeScore(metrics.argumentScore) + ")",
            metrics.nestingLevels + " (" + ColorizeScore(metrics.nestingScore) + ")",
            metrics.returnCount + " (" + ColorizeScore(metrics.returnScore) + ")",
            FormatPureStatus(metrics.IsPure)
        };

        if (hasCoverageData)
        {
            rowData.Add(FormatCoverage(metrics));
        }

        table.AddRow(rowData.ToArray());

        return table;
    }

    /// <summary>
    /// Formats the pure method status with a checkmark or cross icon.
    /// </summary>
    /// <param name="isPure">True if the method is pure, false otherwise</param>
    /// <returns>Formatted string with UTF-8 icon</returns>
    private static string FormatPureStatus(bool isPure)
    {
        return isPure
            ? "[green]✓[/]"  // Green checkmark
            : "[red]✗[/]";    // Red cross
    }

    /// <summary>
    /// Formats coverage percentages for display.
    /// Shows both line and branch coverage if available, or "n/a" if no coverage data.
    /// </summary>
    /// <param name="metrics">The cognitive metrics with coverage data</param>
    /// <returns>Formatted string with coverage percentages, or "n/a" if no coverage</returns>
    private static string FormatCoverage(CognitiveMetrics metrics)
    {
        bool hasLineCoverage = metrics.LineCoveragePercentage.HasValue;
        bool hasBranchCoverage = metrics.BranchCoveragePercentage.HasValue;

        if (!hasLineCoverage && !hasBranchCoverage)
        {
            return "[dim]n/a[/]";
        }

        var parts = new List<string>();

        if (hasLineCoverage)
        {
            double lineCoverage = metrics.LineCoveragePercentage!.Value;
            string lineColor = GetCoverageColor(lineCoverage);
            parts.Add($"[{lineColor}]Line: {lineCoverage:F1}%[/]");
        }

        if (hasBranchCoverage)
        {
            double branchCoverage = metrics.BranchCoveragePercentage!.Value;
            string branchColor = GetCoverageColor(branchCoverage);
            parts.Add($"[{branchColor}]Branch: {branchCoverage:F1}%[/]");
        }

        return string.Join(" | ", parts);
    }

    /// <summary>
    /// Gets the color for coverage percentage based on thresholds.
    /// Green >= 80%, Yellow >= 50%, Red < 50%
    /// </summary>
    /// <param name="coverage">Coverage percentage (0-100)</param>
    /// <returns>Color name for markup</returns>
    private static string GetCoverageColor(double coverage)
    {
        if (coverage >= 80.0)
        {
            return "green";
        }
        else if (coverage >= 50.0)
        {
            return "yellow";
        }
        else
        {
            return "red";
        }
    }

    /// <summary>
    /// Colorizes the metric score based on thresholds.
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
        table.AddColumn("Pure");

        if (hasCoverageData)
        {
            table.AddColumn("Coverage");
        }

        // Center the Pure column
        int pureColumnIndex = hasCoverageData ? table.Columns.Count - 2 : table.Columns.Count - 1;
        table.Columns[pureColumnIndex].Alignment = Justify.Center;

        return table;
    }

    private void RenderSummary(CognitiveMetricsCollection metricsCollection)
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
