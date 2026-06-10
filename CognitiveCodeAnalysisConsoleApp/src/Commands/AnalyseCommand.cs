/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.ComponentModel;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysisConsoleApp.Progress;

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

        [Description("Report type: ConsoleText, Html, Json, Sarif, GithubActions, GitlabCodeQuality, Csv. Defaults to console.")]
        [CommandOption("-r|--report-type|--report-format")]
        [DefaultValue("ConsoleText")]
        public string? ReportType { get; init; }

        [Description("Path to a JSON baseline snapshot for delta comparison")]
        [CommandOption("-b|--baseline")]
        public string? BaselineFile { get; init; }

        [Description("Output file")]
        [CommandOption("-o|--output-file")]
        public string? OutputFile { get; init; }

        [Description("Path to Cobertura coverage report file")]
        [CommandOption("--coverage-cobertura")]
        public string? CoverageCobertura { get; init; }

        [Description("Show Halstead volume/difficulty/effort in reports (overrides config when set)")]
        [CommandOption("--show-halstead")]
        public bool? ShowHalstead { get; init; }

        [Description("Show cyclomatic complexity in reports (overrides config when set)")]
        [CommandOption("--show-cyclomatic")]
        public bool? ShowCyclomatic { get; init; }

        [Description("Show class coupling metrics in reports (overrides config when set)")]
        [CommandOption("--show-coupling")]
        public bool? ShowCoupling { get; init; }
    }

    public override int Execute(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken
    ) {
        try {
            var configuration = ConfigurationLoader.Load(settings.ConfigFile);
            ApplyCliDisplayOverrides(settings, configuration);

            var sourcePath = settings.SourcePath ?? Directory.GetCurrentDirectory();
            var absoluteSourcePath = Path.GetFullPath(sourcePath);
            var reportType = settings.ReportType ?? "ConsoleText";
            var isConsoleText = string.Equals(reportType, "ConsoleText", StringComparison.OrdinalIgnoreCase);

            CognitiveMetricsCollection? metricsCollection = null;
            var filesNotFound = false;
            var coverageFailed = false;
            CognitiveBaselineComparison? baselineComparison = null;

            SpectreProgressSession.Run((reporter, progress) =>
            {
                var files = cognitiveAnalysisFacade.FindSourceFiles(absoluteSourcePath, progress);
                if (!FilesWereFound(files, absoluteSourcePath))
                {
                    filesNotFound = true;
                    return;
                }

                metricsCollection = cognitiveAnalysisFacade.AnalyseSourceFiles(files, configuration, progress);

                if (isConsoleText)
                {
                    return;
                }

                if (!TryApplyCoverage(settings, metricsCollection!, out coverageFailed))
                {
                    return;
                }

                baselineComparison = LoadBaselineComparison(settings, metricsCollection!);

                GenerateReport(
                    settings: settings,
                    configuration: configuration,
                    metricsCollection: metricsCollection!,
                    baselineComparison: baselineComparison,
                    progress: progress,
                    progressReporter: reporter
                );
            });

            if (filesNotFound || coverageFailed) return Error;

            if (isConsoleText)
            {
                if (!HandleCoverage(settings, metricsCollection!)) return Error;

                baselineComparison = LoadBaselineComparison(settings, metricsCollection!);

                GenerateReport(
                    settings: settings,
                    configuration: configuration,
                    metricsCollection: metricsCollection!,
                    baselineComparison: baselineComparison
                );
            }

            return Success;
        } catch (Exception exception) {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(exception.Message)}[/]");
            return Error;
        }
    }

    private bool TryApplyCoverage(Settings settings, CognitiveMetricsCollection metricsCollection, out bool failed)
    {
        failed = false;

        if (string.IsNullOrEmpty(settings.CoverageCobertura))
        {
            return true;
        }

        if (HandleCoverage(settings, metricsCollection))
        {
            return true;
        }

        failed = true;
        return false;
    }

    private static CognitiveBaselineComparison? LoadBaselineComparison(
        Settings settings,
        CognitiveMetricsCollection metricsCollection
    ) {
        if (string.IsNullOrWhiteSpace(settings.BaselineFile))
        {
            return null;
        }

        var baseline = BaselineLoader.Load(settings.BaselineFile);
        return BaselineComparer.Compare(metricsCollection, baseline);
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

    private static void ApplyCliDisplayOverrides(Settings settings, CognitiveConfiguration configuration)
    {
        if (settings.ShowHalstead.HasValue)
        {
            configuration.ShowHalsteadComplexity = settings.ShowHalstead.Value;
        }

        if (settings.ShowCyclomatic.HasValue)
        {
            configuration.ShowCyclomaticComplexity = settings.ShowCyclomatic.Value;
        }

        if (settings.ShowCoupling.HasValue)
        {
            configuration.ShowCouplingMetrics = settings.ShowCoupling.Value;
        }
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
        CognitiveMetricsCollection metricsCollection,
        CognitiveBaselineComparison? baselineComparison,
        IProgress<AnalysisProgress>? progress = null,
        SpectreAnalysisProgressReporter? progressReporter = null
    ) {
        var reportType = settings.ReportType ?? "ConsoleText";
        var outputFile = settings.OutputFile ?? "cognitive-analysis-report";

        if (progressReporter == null)
        {
            reportCoordinator.ReportGenerated += OnReportGenerated;
        }

        try
        {
            reportCoordinator.GenerateReport(
                reportType,
                outputFile,
                configuration,
                metricsCollection,
                baselineComparison,
                progress
            );

            if (progressReporter != null)
            {
                progressReporter.DeferReportGeneratedMessage(
                    reportType,
                    Path.GetFullPath(outputFile)
                );
            }
        }
        finally
        {
            if (progressReporter == null)
            {
                reportCoordinator.ReportGenerated -= OnReportGenerated;
            }
        }
    }

    private static void OnReportGenerated(object? sender, ReportGeneratedEventArgs eventArgs)
    {
        AnsiConsole.MarkupLine($"[green]{eventArgs.ReportType} report generated:[/] {Markup.Escape(eventArgs.FullPath)}");
    }
}
