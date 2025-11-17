using System.Collections.ObjectModel;
using System.Linq;

using Spectre.Console;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public class ConsoleTextReport : ReportInterface
{
    private readonly bool _groupByClass;

    public ConsoleTextReport(bool groupByClass = true)
    {
        _groupByClass = groupByClass;
    }

    public void RenderMetrics(Collection<CognitiveMetrics> metricsCollection)
    {
        if (_groupByClass)
        {
            RenderMetricsGrouped(metricsCollection);
            return;
        }

        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            RenderMetrics(metrics);
        }
    }

    private void RenderMetricsGrouped(Collection<CognitiveMetrics> metricsCollection)
    {
        var groupedByClass = metricsCollection
            .GroupBy(m => new { m.ClassName, m.FilePath })
            .OrderBy(g => g.Key.ClassName);

        foreach (var classGroup in groupedByClass)
        {
            var classMetrics = classGroup.ToList();
            var firstMetric = classMetrics.First();

            AnsiConsole.MarkupLine($"[blue]Class:[/] {Markup.Escape(firstMetric.ClassName)}");
            AnsiConsole.MarkupLine($"[yellow]File:[/] {Markup.Escape(firstMetric.FilePath)}");

            Table table = new();
            table = AddTableHeaders(table);
            table.ShowRowSeparators();

            foreach (var metrics in classMetrics)
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

    private static Table AddTableRow(Table table, CognitiveMetrics metrics)
    {
        table.AddRow(
            "L" + metrics.methodLineNumber.ToString() + " " + Markup.Escape(metrics.MethodName),
            ColorizeScore(metrics.TotalScore()),
            metrics.linesOfCode.ToString(),
            metrics.ifCount.ToString() + " (" + metrics.ifScore.ToString("F3") + ")",
            metrics.argumentCount.ToString() + " (" + metrics.argumentScore.ToString("F3") + ")",
            metrics.nestingLevels.ToString() + " (" + metrics.nestingScore.ToString("F3") + ")",
            metrics.returnCount.ToString() + " (" + metrics.returnScore.ToString("F3") + ")"
        );

        return table;
    }

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
}
