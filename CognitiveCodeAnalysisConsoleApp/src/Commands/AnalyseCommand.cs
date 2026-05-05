/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.ComponentModel;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

using Spectre.Console;
using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Commands;

internal sealed class AnalyseCommand(
    CognitiveAnalysisFacade cognitiveAnalysisFacade,
    ReportCoordinator reportCoordinator
) : Command<AnalyseCommand.Settings> {

    private const int Success = 0;
    private const int Error = -1;

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

        [Description("Path to Cobertura coverage report file")]
        [CommandOption("--coverage-cobertura")]
        public string? CoverageCobertura { get; init; }
    }

    public override int Execute(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    ) {
        try {
            var configuration = ConfigurationLoader.Load();
            var sourcePath = settings.SourcePath ?? Directory.GetCurrentDirectory();
            var absoluteSourcePath = Path.GetFullPath(sourcePath);
            var files = cognitiveAnalysisFacade.FindSourceFiles(absoluteSourcePath);

            if (!FilesWereFound(files , absoluteSourcePath)) return Error;

            var metricsCollection = cognitiveAnalysisFacade.AnalyseSourceFiles(files);

            if (!HandleCoverage(settings , metricsCollection)) return Error;

            GenerateReport(
                settings: settings ,
                configuration: configuration ,
                metricsCollection: metricsCollection
            );

            return Success;
        } catch (Exception exception) {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(exception.Message)}[/]");
            return Error;
        }
    }

    private bool HandleCoverage(Settings settings, CognitiveMetricsCollection metricsCollection)
    {
        if (string.IsNullOrEmpty(settings.CoverageCobertura)) return true;

        var result = cognitiveAnalysisFacade.LoadCoverageData(
            coverageFilePath: settings.CoverageCobertura,
            metricsCollection: metricsCollection
        );

        if (!result.Success) {
            AnsiConsole.MarkupLine($"[yellow]Warning: {result.ErrorMessage}[/]");
        }

        return result.Success;
    }

    private static bool FilesWereFound(List<string> files, string absoluteSourcePath)
    {
        if (files.Count > 0) {
            return true;
        }

        AnsiConsole.MarkupLine($"[yellow]No C# files found in {absoluteSourcePath}.[/]");

        return false;
    }

    private void GenerateReport(
        Settings settings,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection metricsCollection
    ) {
        var reportType = settings.ReportType ?? "ConsoleText";
        var outputFile = settings.OutputFile ?? "cognitive-analysis-report";

        reportCoordinator.ReportGenerated += OnReportGenerated;
        reportCoordinator.GenerateReport(
            reportType,
            outputFile,
            configuration,
            metricsCollection
        );
    }

    private static void OnReportGenerated(object? sender, ReportGeneratedEventArgs eventArgs)
    {
        AnsiConsole.MarkupLine($"[green]{eventArgs.ReportType} report generated:[/] {Markup.Escape(eventArgs.FullPath)}");
    }
}
