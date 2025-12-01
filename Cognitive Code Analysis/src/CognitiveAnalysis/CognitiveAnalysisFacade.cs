using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class CognitiveAnalysisFacade(
    FileFinder fileFinder,
    CognitiveCodeAnalyser analyser,
    CognitiveConfiguration cognitiveConfiguration,
    ScoreCalculator calculator
)
{
    public List<string> FindFiles(string sourcePath)
    {
        return fileFinder.Find([sourcePath]);
    }

    public CognitiveMetricsCollection AnalyseCsharpFiles(
        List<string> files
    )
    {
        CognitiveMetricsCollection metricsCollection = analyser.AnalyseFiles(files, cognitiveConfiguration);

        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            calculator.CalculateScores(metrics);
            CognitiveCodeAnalyser.CalculateTotalScore(metrics);
        }

        return metricsCollection;
    }

    public CognitiveMetrics CalculateScores(CognitiveMetrics metrics)
    {
        return calculator.CalculateScores(metrics);
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
            CoberturaReader reader = new();
            IEnumerable<Coverage> coverageData = reader.ReadCoverage(coverageFilePath);
            var coverageList = coverageData.ToList();

            Dictionary<CognitiveMetrics, Coverage> matches = CoverageMatcher.MatchCoverageToMetrics(
                metricsCollection,
                coverageList
            );

            foreach ((CognitiveMetrics? metrics, Coverage? coverage) in matches)
            {
                metrics.LineCoveragePercentage = coverage.LineCoveragePercentage;
                metrics.BranchCoveragePercentage = coverage.BranchCoveragePercentage;

                // Calculate churn score when coverage data is available
                if (metrics.LineCoveragePercentage.HasValue || metrics.BranchCoveragePercentage.HasValue)
                {
                    metrics.ChurnScore = ChurnCalculator.CalculateChurnScore(metrics);
                }
            }

            if (matches.Count > 0)
            {
                //AnsiConsole.MarkupLine($"[green]Matched coverage data for {matches.Count} method(s)[/]");
                //return;
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
