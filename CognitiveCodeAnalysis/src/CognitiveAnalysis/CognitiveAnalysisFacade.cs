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
        CompiledSourceSet sources = await CompiledSourceSet.BuildAsync(files);

        var cognitiveTask = Task.Run(() => analyser.AnalyseCompiled(sources, configuration, progress));
        var couplingTask = Task.Run(() => classCouplingAnalyser.AnalyseCompiled(sources));

        await Task.WhenAll(cognitiveTask, couplingTask);

        CognitiveMetricsCollection metricsCollection = await cognitiveTask;

        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            calculator.CalculateScores(metrics, configuration);
        }

        metricsCollection.SetClassCouplingMetrics(await couplingTask);

        return metricsCollection;
    }

    public class CoverageLoadingResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public CoverageLoadingResult LoadCoverageData(string coverageFilePath, CognitiveMetricsCollection metricsCollection)
    {
        try
        {
            IEnumerable<Coverage> coverageData = coverageReader.ReadCoverage(coverageFilePath);
            var coverageList = coverageData.ToList();

            Dictionary<CognitiveMetrics, Coverage> matches = CoverageMatcher.MatchCoverageToMetrics(
                metricsCollection,
                coverageList
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
}
