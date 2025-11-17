using System.Collections.ObjectModel;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis;

public static class Program
{
    public static void Main(string[] args)
    {
        FileFinder finder = new FileFinder();
        ConsoleTextReport reporter = new ConsoleTextReport();
        CognitiveConfiguration configuration = ConfigurationLoader.Load();
        ScoreCalculator calculator = new ScoreCalculator(configuration);

        string fixturesPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Cognitive Code Analysis.Tests", "Fixtures");
        string absoluteFixturesPath = Path.GetFullPath(fixturesPath);

        Collection<CognitiveMetrics> metricsCollection = finder.Find([absoluteFixturesPath]);

        // Calculate scores for each metric
        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            calculator.CalculateScores(metrics);
        }

        reporter.RenderMetrics(metricsCollection);
    }
}
