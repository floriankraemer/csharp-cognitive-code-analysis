/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;
using CognitiveCodeAnalysisConsoleApp.Progress;

namespace CognitiveCodeAnalysisConsoleApp.Application;

internal sealed class AnalyseApplicationService(
    AnalysisWorkflow analysisWorkflow,
    IReportGenerationService reportGenerationService,
    IConsoleNotifier consoleNotifier
) {
    public AnalyseResult Run(AnalysisRequest request)
    {
        var prepared = analysisWorkflow.Prepare(request);
        consoleNotifier.WriteConfigUsed(prepared.ConfigSource.Display);

        CognitiveMetricsCollection? metricsCollection = null;
        var filesNotFound = false;
        var coverageFailed = false;
        CognitiveBaselineComparison? baselineComparison = null;

        SpectreProgressSession.Run((reporter, progress) =>
        {
            var files = analysisWorkflow.FindSourceFiles(prepared.AbsoluteSourcePath, progress);
            if (files.Count == 0)
            {
                filesNotFound = true;
                consoleNotifier.WriteNoSourceFilesFound(prepared.AbsoluteSourcePath);
                return;
            }

            metricsCollection = analysisWorkflow.AnalyseSourceFiles(
                files,
                prepared.Configuration,
                progress
            );

            if (prepared.IsConsoleTextReport)
            {
                return;
            }

            if (!TryApplyCoverage(prepared, metricsCollection, out coverageFailed))
            {
                return;
            }

            baselineComparison = analysisWorkflow.CompareBaselineIfRequested(
                prepared.BaselineFile,
                metricsCollection
            );

            reportGenerationService.GenerateReport(
                prepared: prepared,
                metricsCollection: metricsCollection,
                baselineComparison: baselineComparison,
                progress: progress,
                progressReporter: reporter
            );
        });

        if (filesNotFound || coverageFailed)
        {
            return new AnalyseResult(filesNotFound ? AnalyseOutcome.NoSourceFiles : AnalyseOutcome.CoverageFailed);
        }

        if (prepared.IsConsoleTextReport)
        {
            if (!TryApplyCoverage(prepared, metricsCollection!, out _))
            {
                return new AnalyseResult(AnalyseOutcome.CoverageFailed);
            }

            baselineComparison = analysisWorkflow.CompareBaselineIfRequested(
                prepared.BaselineFile,
                metricsCollection!
            );

            reportGenerationService.GenerateReport(
                prepared: prepared,
                metricsCollection: metricsCollection!,
                baselineComparison: baselineComparison
            );
        }

        return new AnalyseResult(AnalyseOutcome.Success);
    }

    private bool TryApplyCoverage(
        PreparedAnalysis prepared,
        CognitiveMetricsCollection metricsCollection,
        out bool failed
    ) {
        failed = false;

        var result = analysisWorkflow.ApplyCoverageIfRequested(
            prepared.CoverageCobertura,
            metricsCollection
        );

        if (!result.Success && result.WarningMessage is { } warningMessage)
        {
            consoleNotifier.WriteWarning(warningMessage);
        }

        if (!result.Success)
        {
            failed = true;
            return false;
        }

        return true;
    }
}
