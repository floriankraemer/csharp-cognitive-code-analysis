using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class CognitiveAnalysisFacade(
    SourceFileFinder sourceFileFinder,
    CognitiveCodeAnalyser analyser,
    CognitiveConfiguration cognitiveConfiguration,
    ScoreCalculator calculator,
    ICoverageReader coverageReader
) {
    public List<string> FindSourceFiles(string[] sourcePaths)
    {
        return sourceFileFinder.FindSourceFiles(sourcePaths);
    }

    public List<string> FindSourceFiles(string sourcePath)
    {
        return sourceFileFinder.FindSourceFiles([sourcePath]);
    }

    public  CognitiveMetricsCollection AnalyseSourceFiles(
        List<string> files
    ) {
        CognitiveMetricsCollection metricsCollection = analyser
            .AnalyseFilesAsync(files, cognitiveConfiguration)
            .GetAwaiter()
            .GetResult();

        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            calculator.CalculateScores(metrics);
        }

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
