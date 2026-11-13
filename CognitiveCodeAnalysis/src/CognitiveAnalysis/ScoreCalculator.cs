/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class ScoreCalculator
{
    private sealed record MetricAccessor(Func<CognitiveMetrics, double> Count, Action<CognitiveMetrics, double> SetScore);

    // Single table of the metric keys a configuration may score; the config loader validates against it.
    private static readonly Dictionary<string, MetricAccessor> Accessors = new(StringComparer.Ordinal)
    {
        ["linesOfCode"] = new(m => m.linesOfCode, (m, s) => m.linesOfCodeScore = s),
        ["ifCount"] = new(m => m.ifCount, (m, s) => m.ifScore = s),
        ["elseCount"] = new(m => m.elseCount, (m, s) => m.elseScore = s),
        ["loopCount"] = new(m => m.loopCount, (m, s) => m.loopScore = s),
        ["switchCount"] = new(m => m.switchCount, (m, s) => m.switchScore = s),
        ["tryCatchCount"] = new(m => m.tryCatchCount, (m, s) => m.tryCatchScore = s),
        ["returnCount"] = new(m => m.returnCount, (m, s) => m.returnScore = s),
        ["argumentCount"] = new(m => m.argumentCount, (m, s) => m.argumentScore = s),
        ["nestingLevels"] = new(m => m.nestingLevels, (m, s) => m.nestingScore = s),
        ["localVariableCount"] = new(m => m.localVariableCount, (m, s) => m.localVariableScore = s),
        ["fieldAccessCount"] = new(m => m.fieldAccessCount, (m, s) => m.fieldAccessScore = s),
        ["propertyAccessCount"] = new(m => m.propertyAccessCount, (m, s) => m.propertyAccessScore = s),
    };

    /// <summary>
    /// The metric keys that can appear in the <c>metrics</c> section of a configuration.
    /// </summary>
    public static IReadOnlyCollection<string> SupportedMetricKeys => Accessors.Keys;

    public CognitiveMetrics CalculateScores(CognitiveMetrics metrics, CognitiveConfiguration configuration)
    {
        foreach (KeyValuePair<string, MetricConfiguration> keyValuePair in configuration.Metrics)
        {
            metrics = CalculateMetric(metrics, keyValuePair);
            metrics = CalculateTotalScore(metrics);
        }

        return metrics;
    }

    private static CognitiveMetrics CalculateMetric(
        CognitiveMetrics metrics,
        KeyValuePair<string, MetricConfiguration> keyValuePair
    ) {
        MetricConfiguration settings = keyValuePair.Value;
        if (!settings.Enabled)
        {
            return metrics;
        }

        // Unknown keys are dropped (with a warning) when a configuration is loaded; programmatic ones are ignored here.
        if (!Accessors.TryGetValue(keyValuePair.Key, out MetricAccessor? accessor))
        {
            return metrics;
        }

        double score = CalculateLogWeight(
            value: accessor.Count(metrics),
            threshold: settings.Threshold,
            scale: settings.Scale
        );
        accessor.SetScore(metrics, score);

        return metrics;
    }

    private static double CalculateLogWeight(double value, double threshold, double scale = 1.0)
    {
        if (value <= threshold) return 0.0;

        return Math.Log(1 + (value - threshold) / scale);
    }

    private static CognitiveMetrics CalculateTotalScore(CognitiveMetrics metrics)
    {
        metrics.totalScore =
            metrics.ifScore +
            metrics.elseScore +
            metrics.loopScore +
            metrics.switchScore +
            metrics.tryCatchScore +
            metrics.returnScore +
            metrics.argumentScore +
            metrics.nestingScore +
            metrics.linesOfCodeScore +
            metrics.localVariableScore +
            metrics.fieldAccessScore +
            metrics.propertyAccessScore;

        return metrics;
    }
}
