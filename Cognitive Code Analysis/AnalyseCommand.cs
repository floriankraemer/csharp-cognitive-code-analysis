using System.Collections.ObjectModel;
using System.ComponentModel;
using CognitiveCodeAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CognitiveCodeAnalysis;

internal sealed class AnalyseCommand : Command<AnalyseCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to search. Defaults to Fixtures folder.")]
        [CommandArgument(0, "[searchPath]")]
        public string? SearchPath { get; init; }

        [CommandOption("-p|--pattern")]
        public string? SearchPattern { get; init; }

        [CommandOption("--hidden")]
        [DefaultValue(true)]
        public bool IncludeHidden { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        FileFinder finder = new FileFinder();
        CognitiveConfiguration configuration = ConfigurationLoader.Load();
        ConsoleTextReport reporter = new ConsoleTextReport(configuration.GroupByClass);
        ScoreCalculator calculator = new ScoreCalculator(configuration);

        var searchPath = settings.SearchPath ?? GetDefaultFixturesPath();
        string absoluteSearchPath = Path.GetFullPath(searchPath);

        AnsiConsole.MarkupLine($"[cyan]Analyzing C# files in:[/] [green]{Markup.Escape(absoluteSearchPath)}[/]");
        AnsiConsole.WriteLine();

        Collection<CognitiveMetrics> metricsCollection = finder.Find([absoluteSearchPath]);

        if (metricsCollection.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No C# files found in the specified directory.[/]");
            return 0;
        }

        // Calculate scores for each metric
        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            calculator.CalculateScores(metrics);
        }

        reporter.RenderMetrics(metricsCollection);

        return 0;
    }

    private static string GetDefaultFixturesPath()
    {
        string fixturesPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Cognitive Code Analysis.Tests", "Fixtures");
        return Path.GetFullPath(fixturesPath);
    }
}
