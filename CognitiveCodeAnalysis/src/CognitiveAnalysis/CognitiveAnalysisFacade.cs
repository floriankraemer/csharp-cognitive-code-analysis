/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
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
    {
        return sourceFileFinder.FindSourceFiles(sourcePaths);
    }

    public List<string> FindSourceFiles(string sourcePath)
    {
        return sourceFileFinder.FindSourceFiles([sourcePath]);
    }

    public CognitiveMetricsCollection AnalyseSourceFiles(List<string> files)
        => AnalyseSourceFiles(files, cognitiveConfiguration);

    public CognitiveMetricsCollection AnalyseSourceFiles(
        List<string> files,
        CognitiveConfiguration configuration
    ) {
        CognitiveMetricsCollection metricsCollection = analyser
            .AnalyseFilesAsync(files, configuration)
            .GetAwaiter()
            .GetResult();

        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            calculator.CalculateScores(metrics, configuration);
        }

        metricsCollection.SetClassCouplingMetrics(classCouplingAnalyser.Analyse(files));

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

            foreach (KeyValuePair<CognitiveMetrics, Coverage> match in matches)
            {
                CognitiveMetrics metrics = match.Key;
                Coverage coverage = match.Value;
                metrics.lineCoveragePercentage = coverage.LineCoveragePercentage;
                metrics.branchCoveragePercentage = coverage.BranchCoveragePercentage;

                if (metrics.HasCoverageData)
                {
                    metrics.churnScore = ChurnCalculator.CalculateChurnScore(metrics);
                }
            }

            if (matches.Count > 0)
            {
                return new CoverageLoadingResult { Success = true };
            }

            if (coverageList.Count > 0)
            {
                return new CoverageLoadingResult { Success = true };
            }

            return new CoverageLoadingResult { Success = false, ErrorMessage = "No coverage data found" };
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
}
