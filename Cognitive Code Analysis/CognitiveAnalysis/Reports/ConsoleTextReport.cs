using Spectre.Console;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public class ConsoleTextReport(bool groupByClass = true, double scoreThreshold = 0.0) : ReportInterface
{
    public void RenderMetrics(CognitiveMetricsCollection metricsCollection)
    {
        if (groupByClass)
        {
            RenderMetricsGrouped(metricsCollection);
            RenderSummary(metricsCollection);
            return;
        }

        foreach (CognitiveMetrics metrics in metricsCollection)
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
            metrics.returnCount + " (" + ColorizeScore(metrics.returnScore) + ")"
        );

        return table;
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
            + "({metricsCollection.GetMethodsPercentage(scoreThreshold):F1}%)"
        );
        AnsiConsole.MarkupLine(
            $"[yellow]Methods Exceeding Threshold:[/] "
            + $"{metricsCollection.GetMethodsExceedingThreshold(scoreThreshold)} "
            + $"({metricsCollection.GetMethodsPercentage(scoreThreshold):F1}%)"
        );
        AnsiConsole.MarkupLine($"Threshold: {scoreThreshold:F3}");
    }
}
