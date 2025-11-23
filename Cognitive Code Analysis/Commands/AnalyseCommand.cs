using System.ComponentModel;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

using Spectre.Console;
using Spectre.Console.Cli;

namespace CognitiveCodeAnalysis.Commands;

internal sealed class AnalyseCommand : Command<AnalyseCommand.Settings>
{
    private const int Success = 0;
    private const int Error = 0;

    private readonly ReportFactory _reportFactory = new();

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

    public override int Execute(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    ) {
        // How to inject this via DI?
        FileFinder finder = new();
        CognitiveCodeAnalyser analyser = new();
        CognitiveConfiguration configuration = ConfigurationLoader.Load();
        ScoreCalculator calculator = new(configuration);

        string sourcePath = settings.SourcePath ?? Directory.GetCurrentDirectory();
        string absoluteSourcePath = Path.GetFullPath(sourcePath);

        List<string> files = GetFiles(finder, absoluteSourcePath);

        if (!FilesWereFound(files, absoluteSourcePath)) return Error;

        RenderReport(
            settings: settings,
            configuration: configuration,
            metricsCollection: AnalyseCsharpFiles(analyser, configuration, calculator, files)
        );

        return Success;
    }

    private static List<string> GetFiles(FileFinder finder, string absoluteSourcePath)
    {
        AnsiConsole.MarkupLine($"[cyan]Analysing C# files in:[/] [green]{Markup.Escape(absoluteSourcePath)}[/]");
        AnsiConsole.WriteLine();

        return finder.Find([absoluteSourcePath]);
    }

    private static bool FilesWereFound(List<string> files, string absoluteSourcePath)
    {
        if (files.Count > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]No C# files found in {absoluteSourcePath}.[/]");
            return false;
        }

        return true;
    }

    private static CognitiveMetricsCollection AnalyseCsharpFiles(
        CognitiveCodeAnalyser analyser,
        CognitiveConfiguration configuration,
        ScoreCalculator calculator,
        List<string> files
    )
    {
        CognitiveMetricsCollection metricsCollection = analyser.AnalyseFiles(files, configuration);

        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            calculator.CalculateScores(metrics);
            CognitiveCodeAnalyser.CalculateTotalScore(metrics);
        }

        return metricsCollection;
    }

    private void RenderReport(
        Settings settings,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection metricsCollection
    ) {
        string reportType = settings.ReportType ?? "ConsoleText";
        string outputFile = settings.OutputFile ?? "cognitive-analysis-report";

        _reportFactory.ReportGenerated += OnReportGenerated;
        _reportFactory.GenerateReport(reportType, outputFile, configuration, metricsCollection);
    }

    private void OnReportGenerated(object? sender, ReportGeneratedEventArgs e)
    {
        AnsiConsole.MarkupLine($"[green]{e.ReportType} report generated:[/] {Markup.Escape(e.FullPath)}");
    }
}
