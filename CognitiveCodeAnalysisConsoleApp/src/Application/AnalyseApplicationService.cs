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
        IAnalysisTracer tracer = request.Verbose
            ? new TimestampedConsoleTracer()
            : NullAnalysisTracer.Instance;

        var prepared = analysisWorkflow.Prepare(request);
        consoleNotifier.WriteConfigUsed(prepared.ConfigSource.Display);

        if (request.Verbose)
        {
            tracer.Trace(
                $"Run started (report={prepared.ReportType}, coupling={prepared.Configuration.ShowCouplingMetrics}, source={prepared.AbsoluteSourcePath})");
        }

        CognitiveMetricsCollection? metricsCollection = null;
        var filesNotFound = false;
        var coverageFailed = false;
        CognitiveBaselineComparison? baselineComparison = null;

        SpectreProgressSession.Run((reporter, progress) =>
        {
            IProgress<AnalysisProgress> effectiveProgress = request.Verbose
                ? new TracingProgress(progress, tracer)
                : progress;

            List<string> files = tracer.TraceStep(
                "Finding source files",
                () => analysisWorkflow.FindSourceFiles(prepared.AbsoluteSourcePath, effectiveProgress));

            if (files.Count == 0)
            {
                filesNotFound = true;
                consoleNotifier.WriteNoSourceFilesFound(prepared.AbsoluteSourcePath);
                return;
            }

            metricsCollection = tracer.TraceStep(
                $"Analysing {files.Count} source file(s)",
                () => analysisWorkflow.AnalyseSourceFiles(
                    files,
                    prepared.Configuration,
                    effectiveProgress
                ));

            if (prepared.IsConsoleTextReport)
            {
                return;
            }

            if (!TryApplyCoverage(prepared, metricsCollection, effectiveProgress, out coverageFailed))
            {
                return;
            }

            baselineComparison = string.IsNullOrWhiteSpace(prepared.BaselineFile)
                ? null
                : tracer.TraceStep(
                    "Comparing baseline",
                    () => analysisWorkflow.CompareBaselineIfRequested(
                        prepared.BaselineFile,
                        metricsCollection,
                        effectiveProgress
                    ));

            tracer.TraceStep(
                $"Writing {prepared.ReportType} report",
                () =>
                {
                    reportGenerationService.GenerateReport(
                        prepared: prepared,
                        metricsCollection: metricsCollection,
                        baselineComparison: baselineComparison,
                        progress: effectiveProgress,
                        progressReporter: reporter
                    );
                });
        });

        if (filesNotFound || coverageFailed)
        {
            return new AnalyseResult(filesNotFound ? AnalyseOutcome.NoSourceFiles : AnalyseOutcome.CoverageFailed);
        }

        if (prepared.IsConsoleTextReport)
        {
            IProgress<AnalysisProgress>? consoleProgress = request.Verbose
                ? new TracingProgress(NullProgress.Instance, tracer)
                : null;

            if (!TryApplyCoverage(prepared, metricsCollection!, consoleProgress, out _))
            {
                return new AnalyseResult(AnalyseOutcome.CoverageFailed);
            }

            baselineComparison = analysisWorkflow.CompareBaselineIfRequested(
                prepared.BaselineFile,
                metricsCollection!,
                consoleProgress
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
        IProgress<AnalysisProgress>? progress,
        out bool failed
    ) {
        failed = false;

        if (string.IsNullOrEmpty(prepared.CoverageCobertura))
        {
            return true;
        }

        var result = analysisWorkflow.ApplyCoverageIfRequested(
            prepared.CoverageCobertura,
            metricsCollection,
            progress
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

    private sealed class NullProgress : IProgress<AnalysisProgress>
    {
        public static readonly NullProgress Instance = new();

        public void Report(AnalysisProgress value)
        {
        }
    }
}
