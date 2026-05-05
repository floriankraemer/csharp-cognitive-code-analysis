/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

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
