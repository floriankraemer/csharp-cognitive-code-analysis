using System.Collections.ObjectModel;

using CognitiveCodeAnalysis.CognitiveAnalysis;

using Spectre.Console;

namespace CognitiveCodeAnalysis
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            FileFinder finder = new FileFinder();

            Collection<CognitiveMetrics> metricsCollection = finder.Find(["C:\\Users\\flori\\source\\repos\\cSharp-Cognitive-Code-Analysis\\Cognitive Code Analysis\\Cognitive Code Analysis"]);

            foreach (CognitiveMetrics metrics in metricsCollection)
            {
                RenderMetrics(metrics);
            }
        }

        private static void RenderMetrics(CognitiveMetrics metrics)
        {
            AnsiConsole.MarkupLine($"[blue]Class:[/] {Markup.Escape(metrics.ClassName)}");
            AnsiConsole.MarkupLine($"[green]Method:[/] {Markup.Escape(metrics.methodSignature)}");
            AnsiConsole.MarkupLine($"[yellow]File:[/] {Markup.Escape(metrics.FilePath)}");

            var table = new Table();
            table = AddTableHeaders(table);
            table = AddTableRow(table, metrics);

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        private static Table AddTableRow(Table table, CognitiveMetrics metrics)
        {
            return table.AddRow(
                "L" + metrics.methodLineNumber.ToString() + " " + Markup.Escape(metrics.methodSignature),
                metrics.linesOfCode.ToString(),
                metrics.ifCount.ToString() + " (" + metrics.ifScore.ToString() + ")",
                metrics.argumentCount.ToString() + " (" + metrics.argumentScore.ToString() + ")",
                metrics.nestingLevels.ToString() + " (" + metrics.nestingScore.ToString() + ")",
                metrics.returnCount.ToString() + " (" + metrics.returnScore.ToString() + ")"
            );
        }

        private static Table AddTableHeaders(Table table)
        {
            table.AddColumn("Method");
            table.AddColumn("Lines");
            table.AddColumn("Ifs");
            table.AddColumn("Arguments");
            table.AddColumn("Nesting");
            table.AddColumn("Returns");

            return table;
        }
    }
}
