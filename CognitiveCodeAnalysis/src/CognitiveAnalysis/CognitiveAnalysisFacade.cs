/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.Common;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class CognitiveAnalysisFacade(
    SourceFileFinder sourceFileFinder,
    CognitiveCodeAnalyser analyser,
    CognitiveConfiguration cognitiveConfiguration,
    ScoreCalculator calculator,
    ICoverageReader coverageReader,
    ClassCouplingAnalyser classCouplingAnalyser
) {
    public List<string> FindSourceFiles(string[] sourcePaths)
        => sourceFileFinder.FindSourceFiles(sourcePaths);

    public List<string> FindSourceFiles(string[] sourcePaths, IProgress<AnalysisProgress>? progress)
        => sourceFileFinder.FindSourceFiles(sourcePaths, progress);

    public List<string> FindSourceFiles(string sourcePath)
        => sourceFileFinder.FindSourceFiles([sourcePath]);

    public List<string> FindSourceFiles(string sourcePath, IProgress<AnalysisProgress>? progress)
        => sourceFileFinder.FindSourceFiles([sourcePath], progress);

    public CognitiveMetricsCollection AnalyseSourceFiles(List<string> files)
        => AnalyseSourceFiles(files, cognitiveConfiguration);

    public CognitiveMetricsCollection AnalyseSourceFiles(
        List<string> files,
        CognitiveConfiguration configuration
    ) => AnalyseSourceFiles(files, configuration, progress: null);

    public CognitiveMetricsCollection AnalyseSourceFiles(
        List<string> files,
        CognitiveConfiguration configuration,
        IProgress<AnalysisProgress>? progress
    ) => AnalyseSourceFilesAsync(files, configuration, progress).GetAwaiter().GetResult();

    private async Task<CognitiveMetricsCollection> AnalyseSourceFilesAsync(
        List<string> files,
        CognitiveConfiguration configuration,
        IProgress<AnalysisProgress>? progress
    ) {
        CompiledSourceSet sources = await CompiledSourceSet.BuildAsync(files, progress);

        Task<CognitiveMetricsCollection> cognitiveTask = Task.Run(
            () => analyser.AnalyseCompiled(sources, configuration, progress)
        );

        Task<IReadOnlyList<ClassCouplingMetrics>>? couplingTask = null;
        if (configuration.ShowCouplingMetrics)
        {
            couplingTask = Task.Run(() => classCouplingAnalyser.AnalyseCompiled(sources, progress));
            await Task.WhenAll(cognitiveTask, couplingTask);
        }
        else
        {
            await cognitiveTask;
        }

        CognitiveMetricsCollection metricsCollection = await cognitiveTask;

        CalculateScoresWithProgress(metricsCollection, configuration, progress);

        if (couplingTask != null)
        {
            metricsCollection.SetClassCouplingMetrics(await couplingTask);
        }

        return metricsCollection;
    }

    private void CalculateScoresWithProgress(
        CognitiveMetricsCollection metricsCollection,
        CognitiveConfiguration configuration,
        IProgress<AnalysisProgress>? progress
    ) {
        int totalMethods = metricsCollection.Count;
        if (totalMethods == 0)
        {
            progress?.Report(new AnalysisProgress(
                AnalysisProgressPhase.CalculatingScores,
                TotalFiles: 0,
                ProcessedFiles: 0
            ));
            progress?.Report(new AnalysisProgress(
                AnalysisProgressPhase.ScoresCalculated,
                TotalFiles: 0,
                ProcessedFiles: 0
            ));
            return;
        }

        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.CalculatingScores,
            TotalFiles: totalMethods,
            ProcessedFiles: 0
        ));

        const int progressBatchSize = 100;
        int processedMethods = 0;

        Parallel.ForEach(
            metricsCollection,
            metrics =>
            {
                calculator.CalculateScores(metrics, configuration);
                int count = Interlocked.Increment(ref processedMethods);
                if (count % progressBatchSize == 0 || count == totalMethods)
                {
                    progress?.Report(new AnalysisProgress(
                        AnalysisProgressPhase.CalculatingScores,
                        TotalFiles: totalMethods,
                        ProcessedFiles: count
                    ));
                }
            });

        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.ScoresCalculated,
            TotalFiles: totalMethods,
            ProcessedFiles: totalMethods
        ));
    }

    public CoverageLoadingResult LoadCoverageData(
        string coverageFilePath,
        CognitiveMetricsCollection metricsCollection
    ) => LoadCoverageData(coverageFilePath, metricsCollection, progress: null);

    public CoverageLoadingResult LoadCoverageData(
        string coverageFilePath,
        CognitiveMetricsCollection metricsCollection,
        IProgress<AnalysisProgress>? progress,
        CoverageFormat format = CoverageFormat.Auto
    )
    {
        try
        {
            IEnumerable<Coverage> coverageData = format.ReaderFor(coverageReader).ReadCoverage(coverageFilePath);
            var coverageList = coverageData.ToList();

            Dictionary<CognitiveMetrics, Coverage> matches = CoverageMatcher.MatchCoverageToMetrics(
                metricsCollection,
                coverageList,
                progress
            );

            foreach ((CognitiveMetrics? metrics, Coverage? coverage) in matches)
            {
                metrics.lineCoveragePercentage = coverage.LineCoveragePercentage;
                metrics.branchCoveragePercentage = coverage.BranchCoveragePercentage;

                if (metrics.HasCoverageData)
                {
                    metrics.churnScore = ChurnCalculator.CalculateChurnScore(metrics);
                }
            }

            if (matches.Count > 0)
            {
                return new CoverageLoadingResult { Success = true, WarningMessage = PartialMatchWarning(coverageFilePath, metricsCollection, matches) };
            }

            if (coverageList.Count > 0)
            {
                return new CoverageLoadingResult
                {
                    Success = true,
                    WarningMessage = $"Loaded {coverageList.Count} coverage entries from {coverageFilePath} but none matched an analysed method. "
                        + "Check that the coverage report was produced for the analysed sources and that its file paths resolve to them.",
                };
            }

            return new CoverageLoadingResult { Success = false, ErrorMessage = "No coverage data found" };
        }
        catch (InvalidCoverageFileException exception)
        {
            return new CoverageLoadingResult { Success = false, ErrorMessage = exception.Message };
        }
        catch (FileNotFoundException exception)
        {
            return new CoverageLoadingResult { Success = false, ErrorMessage = $"Coverage file not found: {exception.FileName ?? coverageFilePath}" };
        }
        catch (Exception exception)
        {
            return new CoverageLoadingResult { Success = false, ErrorMessage = $"Failed to load coverage data: {exception.Message}" };
        }
    }

    /// <summary>
    /// A report that knows only some of the analysed classes was probably produced from other sources or another module.
    /// </summary>
    private static string? PartialMatchWarning(
        string coverageFilePath,
        CognitiveMetricsCollection metricsCollection,
        Dictionary<CognitiveMetrics, Coverage> matches
    ) {
        int analysed = metricsCollection.Select(ClassKey).Distinct().Count();
        int matched = matches.Keys.Select(ClassKey).Distinct().Count();

        return matched < analysed
            ? $"Coverage report {coverageFilePath} matched {matched} of {analysed} analysed classes."
            : null;
    }

    private static (string, string) ClassKey(CognitiveMetrics metrics) => (metrics.FilePath, metrics.ClassName);

    public class CoverageLoadingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Set when loading succeeded but the result is probably not what the caller expects.
        /// </summary>
        public string? WarningMessage { get; set; }
    }
}
