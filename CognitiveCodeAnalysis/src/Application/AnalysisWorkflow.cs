/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Application;

public sealed class AnalysisWorkflow(
    CognitiveAnalysisFacade cognitiveAnalysisFacade,
    BaselineComparisonService baselineComparisonService,
    ReportCoordinator reportCoordinator,
    TimeProvider? timeProvider = null
) {
    public PreparedAnalysis Prepare(AnalysisRequest request)
    {
        var (configuration, configSource) = CognitiveConfigurationFactory.LoadWithSource(
            request.ConfigFile,
            request.DisplayOverrides
        );
        var sorter = MetricsSorter.Create(request.SortBy, request.SortOrder);
        configuration.Sorter = request.SortBy is null ? null : sorter;
        var sourcePaths = SourceFileFinder.SplitPathList(request.SourcePath ?? Directory.GetCurrentDirectory());
        var reportType = request.ReportType;
        var outputFile = Path.GetFullPath(request.OutputFile ?? "cognitive-analysis-report");
        var isConsoleTextReport = string.Equals(reportType, "ConsoleText", StringComparison.OrdinalIgnoreCase);

        // Only a project that already keeps baselines has this directory, so everybody else sees no change.
        string workingDirectory = Directory.GetCurrentDirectory();
        string? baselineFile = ToAbsolutePath(request.BaselineFile);
        bool autoDetected = false;
        string? baselineWarning = null;
        if (string.IsNullOrWhiteSpace(baselineFile))
        {
            baselineFile = BaselineFiles.Latest(workingDirectory, out baselineWarning);
            autoDetected = baselineFile != null;
        }

        return new PreparedAnalysis(
            Configuration: configuration,
            ConfigSource: configSource,
            AbsoluteSourcePath: string.Join(", ", sourcePaths),
            SourcePaths: sourcePaths,
            ReportType: reportType,
            OutputFile: outputFile,
            BaselineFile: baselineFile,
            CoverageCobertura: ToAbsolutePath(request.CoverageCobertura),
            IsConsoleTextReport: isConsoleTextReport,
            BaselineAutoDetected: autoDetected,
            BaselineWarning: baselineWarning,
            CoverageClover: ToAbsolutePath(request.CoverageClover)
        );
    }

    private static string? ToAbsolutePath(string? path) =>
        string.IsNullOrWhiteSpace(path) ? path : Path.GetFullPath(path);

    public List<string> FindSourceFiles(PreparedAnalysis prepared, IProgress<AnalysisProgress>? progress = null)
        => cognitiveAnalysisFacade.FindSourceFiles(
            [.. prepared.SourcePaths],
            prepared.Configuration.ExcludeFilePatterns,
            progress
        );

    public CognitiveMetricsCollection AnalyseSourceFiles(
        List<string> files,
        CognitiveConfiguration configuration,
        IProgress<AnalysisProgress>? progress = null
    ) => cognitiveAnalysisFacade.AnalyseSourceFiles(files, configuration, progress);

    public CoverageApplicationResult ApplyCoverageIfRequested(
        string? coverageFilePath,
        CognitiveMetricsCollection metricsCollection
    ) => ApplyCoverageIfRequested(coverageFilePath, metricsCollection, progress: null);

    public CoverageApplicationResult ApplyCoverageIfRequested(
        string? coverageFilePath,
        CognitiveMetricsCollection metricsCollection,
        IProgress<AnalysisProgress>? progress,
        CoverageFormat format = CoverageFormat.Auto
    ) {
        if (string.IsNullOrEmpty(coverageFilePath))
        {
            return new CoverageApplicationResult(Success: true);
        }

        var result = cognitiveAnalysisFacade.LoadCoverageData(
            coverageFilePath: coverageFilePath,
            metricsCollection: metricsCollection,
            progress: progress,
            format: format
        );

        return new CoverageApplicationResult(
            Success: result.Success,
            WarningMessage: result.Success ? result.WarningMessage : result.ErrorMessage
        );
    }

    public CognitiveBaselineComparison? CompareBaselineIfRequested(
        string? baselineFile,
        CognitiveMetricsCollection metricsCollection
    ) => CompareBaselineIfRequested(baselineFile, metricsCollection, progress: null);

    public CognitiveBaselineComparison? CompareBaselineIfRequested(
        string? baselineFile,
        CognitiveMetricsCollection metricsCollection,
        IProgress<AnalysisProgress>? progress
    ) => baselineComparisonService.CompareIfRequested(baselineFile, metricsCollection, progress);

    /// <summary>The warning when the baseline was scored with other metric settings than the current ones, otherwise null.</summary>
    public static string? BaselineConfigWarning(
        CognitiveBaselineComparison? comparison,
        CognitiveConfiguration configuration
    ) => comparison == null
        ? null
        : BaselineConfigHash.Warning(comparison.BaselineConfigHash, BaselineConfigHash.Of(configuration));

    /// <summary>Writes the metrics as a new baseline file below the working directory and returns its relative path.</summary>
    public string GenerateBaseline(CognitiveConfiguration configuration, CognitiveMetricsCollection metricsCollection)
    {
        var snapshot = BaselineSnapshotFactory.FromMetricsCollection(
            metricsCollection,
            relativePaths: true,
            configHash: BaselineConfigHash.Of(configuration),
            generatedAt: (timeProvider ?? TimeProvider.System).GetUtcNow());
        return BaselineFiles.Generate(Directory.GetCurrentDirectory(), snapshot);
    }

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
