using System.Collections.ObjectModel;

using Spectre.Console;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
public class ConsoleTextReport
{
    public void RenderMetrics(Collection<CognitiveMetrics> metricsCollection)
    {
        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            RenderMetrics(metrics);
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
            "L" + metrics.methodLineNumber.ToString() + " " + Markup.Escape(metrics.methodSignature),
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
