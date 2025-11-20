using System.ComponentModel;
using System.Reflection;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

using Spectre.Console;
using Spectre.Console.Cli;

namespace CognitiveCodeAnalysis.Commands;

internal sealed class AnalyseCommand : Command<AnalyseCommand.Settings>
{
    private const int Success = 0;

    public sealed class Settings : CommandSettings
    {
        [Description("Path to search for c# files. Defaults to current path.")]
        [CommandArgument(0, "[searchPath]")]
        public string? SourcePath { get; init; }

        [Description("Load a custom configuration")]
        [CommandOption("-c|--config")]
        public string? ConfigFile { get; init; }

        [Description("Report type. Defaults to console.")]
        [CommandOption("-r|--report-type")]
        [DefaultValue("ConsoleText")]
        public string? ReportType { get; init; }

        [Description("Output file")]
        [CommandOption("-o|--output-file")]
        public string? OutputFile { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        // How to inject this via DI?
        FileFinder finder = new();
        CognitiveCodeAnalyser analyser = new();
        CognitiveConfiguration configuration = ConfigurationLoader.Load();
        ScoreCalculator calculator = new(configuration);

        string searchPath = settings.SourcePath ?? GetCurrentDirectory();
        string absoluteSearchPath = Path.GetFullPath(searchPath);

        AnsiConsole.MarkupLine($"[cyan]Analyzing C# files in:[/] [green]{Markup.Escape(absoluteSearchPath)}[/]");
        AnsiConsole.WriteLine();

        List<string> files = finder.Find([absoluteSearchPath]);

        if (files.Count == 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No C# files found in {absoluteSearchPath}.[/]");

            return Success;
        }

        RenderReport(
            settings: settings,
            configuration: configuration,
            metricsCollection: AnalyseCsharpFiles(analyser, configuration, calculator, files)
        );

        return Success;
    }

    private static CognitiveMetricsCollection AnalyseCsharpFiles(
        CognitiveCodeAnalyser analyser,
        CognitiveConfiguration configuration,
        ScoreCalculator calculator,
        List<string> files
    ) {
        CognitiveMetricsCollection metricsCollection = analyser.AnalyzeFiles(files, configuration);

        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            calculator.CalculateScores(metrics);
        }

        return metricsCollection;
    }

    private static void RenderReport(
        Settings settings,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection metricsCollection
    ) {
        string reportType = settings.ReportType ?? "ConsoleText";
        string outputFile = settings.OutputFile ?? "cognitive-analysis-report";

        ReportInterface reporter = reportType switch
        {
            "ConsoleText" => new ConsoleTextReport(configuration),
            "Html" => new HtmlReport(outputFile, configuration.GroupByClass),
            _ => throw new ArgumentException($"Invalid report type: {reportType}")
        };

        reporter.RenderMetrics(metricsCollection);

        string fullPath = Path.GetFullPath(outputFile);
        AnsiConsole.MarkupLine($"[green]{reportType} report generated:[/] {Markup.Escape(fullPath)}");
    }

    private static string GetCurrentDirectory()
    {
        string? directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        if (directory == null)
        {
            throw new InvalidOperationException("Unable to determine the executing assembly's directory.");
        }

        return directory;
    }
}
