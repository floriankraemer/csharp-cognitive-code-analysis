/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Application;

public sealed class AnalysisWorkflow(
    CognitiveAnalysisFacade cognitiveAnalysisFacade,
    BaselineComparisonService baselineComparisonService,
    ReportCoordinator reportCoordinator
) {
    public PreparedAnalysis Prepare(AnalysisRequest request)
    {
        var configuration = CognitiveConfigurationFactory.Load(request.ConfigFile, request.DisplayOverrides);
        var sourcePath = request.SourcePath ?? Directory.GetCurrentDirectory();
        var absoluteSourcePath = Path.GetFullPath(sourcePath);
        var reportType = request.ReportType;
        var outputFile = request.OutputFile ?? "cognitive-analysis-report";
        var isConsoleTextReport = string.Equals(reportType, "ConsoleText", StringComparison.OrdinalIgnoreCase);

        return new PreparedAnalysis(
            Configuration: configuration,
            AbsoluteSourcePath: absoluteSourcePath,
            ReportType: reportType,
            OutputFile: outputFile,
            BaselineFile: request.BaselineFile,
            CoverageCobertura: request.CoverageCobertura,
            IsConsoleTextReport: isConsoleTextReport
        );
    }

    public List<string> FindSourceFiles(string absoluteSourcePath, IProgress<AnalysisProgress>? progress = null)
        => cognitiveAnalysisFacade.FindSourceFiles(absoluteSourcePath, progress);

    public CognitiveMetricsCollection AnalyseSourceFiles(
        List<string> files,
        CognitiveConfiguration configuration,
        IProgress<AnalysisProgress>? progress = null
    ) => cognitiveAnalysisFacade.AnalyseSourceFiles(files, configuration, progress);

    public CoverageApplicationResult ApplyCoverageIfRequested(
        string? coverageFilePath,
        CognitiveMetricsCollection metricsCollection
    ) {
        if (string.IsNullOrEmpty(coverageFilePath))
        {
            return new CoverageApplicationResult(Success: true);
        }

        var result = cognitiveAnalysisFacade.LoadCoverageData(
            coverageFilePath: coverageFilePath,
            metricsCollection: metricsCollection
        );

        return new CoverageApplicationResult(
            Success: result.Success,
            WarningMessage: result.Success ? null : result.ErrorMessage
        );
    }

    public CognitiveBaselineComparison? CompareBaselineIfRequested(
        string? baselineFile,
        CognitiveMetricsCollection metricsCollection
    ) => baselineComparisonService.CompareIfRequested(baselineFile, metricsCollection);

    public void GenerateReport(
        string reportType,
        string outputFile,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection metricsCollection,
        CognitiveBaselineComparison? baselineComparison = null,
        IProgress<AnalysisProgress>? progress = null
    ) {
        reportCoordinator.GenerateReport(
            reportType,
            outputFile,
            configuration,
            metricsCollection,
            baselineComparison,
            progress
        );
    }
}
