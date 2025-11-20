using CognitiveCodeAnalysis.Configuration;
using Spectre.Console;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public class ConsoleTextReport(CognitiveConfiguration configuration) : ReportInterface
{
    public void RenderMetrics(CognitiveMetricsCollection metricsCollection)
    {
        // Filter metrics if ShowOnlyMethodsExceedingThreshold is enabled
        CognitiveMetricsCollection filteredCollection = FilterMetrics(metricsCollection);

        if (configuration.GroupByClass)
        {
            RenderMetricsGrouped(filteredCollection);
            RenderSummary(metricsCollection);
            return;
        }

        foreach (CognitiveMetrics metrics in filteredCollection)
        {
            RenderMetrics(metrics);
        }

        RenderSummary(metricsCollection);
    }

    private void RenderMetricsGrouped(CognitiveMetricsCollection metricsCollection)
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
            table = AddTableHeaders(table);
            table.ShowRowSeparators();

            foreach (CognitiveMetrics metrics in classMetrics)
            {
                table = AddTableRow(table, metrics);
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
            if (metrics.TotalScore() > scoreThreshold)
            {
                filtered.Add(metrics);
            }
        }

        return filtered;
    }

    private static void RenderMetrics(CognitiveMetrics metrics)
    {
        RenderMetricsSummary(metrics);

        Table table = new();
        table = AddTableHeaders(table);
        table = AddTableRow(table, metrics);

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static Table AddTableRow(
        Table table,
        CognitiveMetrics metrics
    ) {
        table.AddRow(
            "L" + metrics.methodLineNumber + " " + Markup.Escape(metrics.MethodName),
            ColorizeScore(metrics.TotalScore()),
            metrics.linesOfCode.ToString(),
            metrics.ifCount + " (" + ColorizeScore(metrics.ifScore) + ")",
            metrics.argumentCount + " (" + ColorizeScore(metrics.argumentScore) + ")",
            metrics.nestingLevels + " (" + ColorizeScore(metrics.nestingScore) + ")",
            metrics.returnCount + " (" + ColorizeScore(metrics.returnScore) + ")",
            FormatPureStatus(metrics.IsPure)
        );

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

    private static Table AddTableHeaders(Table table)
    {
        table.AddColumn("Method");
        table.AddColumn("Score");
        table.AddColumn("Lines");
        table.AddColumn("Ifs");
        table.AddColumn("Arguments");
        table.AddColumn("Nesting");
        table.AddColumn("Returns");
        table.AddColumn("Pure");

        // Center the Pure column
        table.Columns[^1].Alignment = Justify.Center;

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
