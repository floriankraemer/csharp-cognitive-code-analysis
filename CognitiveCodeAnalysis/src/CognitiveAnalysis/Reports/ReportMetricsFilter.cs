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

    /// <summary>
    /// Orders methods by <c>--sort-by</c> and otherwise keeps the given order, for reports that have no score order of their own.
    /// </summary>
    public static IEnumerable<CognitiveMetrics> SortIfRequested(
        IEnumerable<CognitiveMetrics> metrics,
        CognitiveConfiguration configuration
    ) => configuration.Sorter?.Sort(metrics) ?? metrics;

    /// <summary>
    /// Orders report rows by <c>--sort-by</c>, or by total score, highest first, when none was requested.
    /// </summary>
    public static IReadOnlyList<CognitiveMetrics> OrderRows(
        IEnumerable<CognitiveMetrics> metrics,
        CognitiveConfiguration configuration
    ) => configuration.Sorter?.Sort(metrics)
        ?? metrics.OrderByDescending(m => m.totalScore).ToList();
}
