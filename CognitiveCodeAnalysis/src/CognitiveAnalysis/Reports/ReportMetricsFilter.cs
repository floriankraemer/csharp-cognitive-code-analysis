using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

/// <summary>
/// Applies the same score-threshold filtering as console text output.
/// </summary>
public static class ReportMetricsFilter
{
    public static CognitiveMetricsCollection FilterForReport(
        CognitiveMetricsCollection metricsCollection,
        CognitiveConfiguration configuration
    )
    {
        if (!configuration.ShowOnlyMethodsExceedingThreshold)
        {
            return metricsCollection;
        }

        return metricsCollection.OnlyMetricsExceedingScoreThreshold(configuration.ScoreThreshold);
    }
}
