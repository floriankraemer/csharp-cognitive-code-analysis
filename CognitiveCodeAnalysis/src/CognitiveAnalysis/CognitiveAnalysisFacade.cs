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
        IProgress<AnalysisProgress>? progress
    )
    {
        try
        {
            IEnumerable<Coverage> coverageData = coverageReader.ReadCoverage(coverageFilePath);
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
                //AnsiConsole.MarkupLine($"[green]Matched coverage data for {matches.Count} method(s)[/]");
                return new CoverageLoadingResult { Success = true };
            }

            if (coverageList.Count > 0)
            {
                //AnsiConsole.MarkupLine($"[yellow]Warning: Loaded {coverageList.Count} coverage entries but found no matches with metrics[/]");
                return new CoverageLoadingResult { Success = true };
            }

            return new CoverageLoadingResult { Success = false, ErrorMessage = "No coverage data found" };
        }
        catch (FileNotFoundException exception)
        {
            //AnsiConsole.MarkupLine($"[yellow]Warning: Coverage file not found:[/] {Markup.Escape(exception.FileName ?? coverageFilePath)}");
            return new CoverageLoadingResult { Success = false, ErrorMessage = $"Coverage file not found: {exception.FileName ?? coverageFilePath}" };
        }
        catch (Exception exception)
        {
            //AnsiConsole.MarkupLine($"[yellow]Warning: Failed to load coverage data:[/] {Markup.Escape(exception.Message)}");
            return new CoverageLoadingResult { Success = false, ErrorMessage = $"Failed to load coverage data: {exception.Message}" };
        }
    }

    public class CoverageLoadingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
