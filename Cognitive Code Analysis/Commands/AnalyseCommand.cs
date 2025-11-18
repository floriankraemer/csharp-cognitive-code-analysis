using System.Collections.ObjectModel;
using System.ComponentModel;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CognitiveCodeAnalysis.Commands;

internal sealed class AnalyseCommand : Command<AnalyseCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to search for c# files. Defaults to Fixtures folder.")]
        [CommandArgument(0, "[searchPath]")]
        public string? SourcePath { get; init; }

        [Description("Load a custom configuration")]
        [CommandOption("-c|--config")]
        public string? ConfigFile { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        FileFinder finder = new();
        CognitiveCodeAnalyser analyser = new ();
        CognitiveConfiguration configuration = ConfigurationLoader.Load();
        ConsoleTextReport reporter = new(configuration.GroupByClass);
        ScoreCalculator calculator = new(configuration);

        var searchPath = settings.SourcePath ?? GetDefaultFixturesPath();
        string absoluteSearchPath = Path.GetFullPath(searchPath);

        AnsiConsole.MarkupLine($"[cyan]Analyzing C# files in:[/] [green]{Markup.Escape(absoluteSearchPath)}[/]");
        AnsiConsole.WriteLine();

        List<string> files = finder.Find([absoluteSearchPath]);

        if (files.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No C# files found in the specified directory.[/]");
            return 0;
        }

        Collection<CognitiveMetrics> metricsCollection = analyser.AnalyzeFiles(files, configuration);

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
