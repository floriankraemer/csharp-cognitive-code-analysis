/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;
using CognitiveCodeAnalysisConsoleApp.Progress;

namespace CognitiveCodeAnalysisConsoleApp.Application;

internal sealed class SpectreReportGenerationService(
    AnalysisWorkflow analysisWorkflow,
    ReportCoordinator reportCoordinator,
    IConsoleNotifier consoleNotifier
) : IReportGenerationService {
    public void GenerateReport(
        PreparedAnalysis prepared,
        CognitiveMetricsCollection metricsCollection,
        CognitiveBaselineComparison? baselineComparison,
        IProgress<AnalysisProgress>? progress = null,
        SpectreAnalysisProgressReporter? progressReporter = null
    ) {
        if (progressReporter == null)
        {
            reportCoordinator.ReportGenerated += OnReportGenerated;
        }

        try
        {
            analysisWorkflow.GenerateReport(
                reportType: prepared.ReportType,
                outputFile: prepared.OutputFile,
                configuration: prepared.Configuration,
                metricsCollection: metricsCollection,
                baselineComparison: baselineComparison,
                progress: progress
            );

            if (progressReporter != null)
            {
                progressReporter.DeferReportGeneratedMessage(
                    prepared.ReportType,
                    Path.GetFullPath(prepared.OutputFile)
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

    private void OnReportGenerated(object? sender, ReportGeneratedEventArgs eventArgs)
        => consoleNotifier.WriteReportGenerated(eventArgs.ReportType, eventArgs.FullPath);
}
