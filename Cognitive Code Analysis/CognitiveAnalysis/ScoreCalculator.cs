using System.Reflection;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class ScoreCalculator(CognitiveConfiguration configuration)
{
    public CognitiveMetrics CalculateScores(CognitiveMetrics metrics)
    {
        foreach (KeyValuePair<string, MetricConfiguration> keyValuePair in configuration.Metrics)
        {
            metrics = CalculateMetric(metrics, keyValuePair);
        }

        return metrics;
    }

    private static CognitiveMetrics CalculateMetric(
        CognitiveMetrics metrics,
        KeyValuePair<string, MetricConfiguration> keyValuePair
    ) {
        if (!keyValuePair.Value.Enabled)
        {
            return metrics;
        }

        string metricField = keyValuePair.Key;

        FieldInfo? countField = metrics.GetType().GetField(metricField);
        if (countField == null)
        {
            return metrics;
        }

        object? countValue = countField.GetValue(metrics);
        if (countValue == null)
        {
            return metrics;
        }

        double count = Convert.ToDouble(countValue);
        double score = CalculateLogWeight(
            value: count,
            threshold: keyValuePair.Value.Threshold,
            scale: keyValuePair.Value.Scale
        );

        // Set score to field with same name but "Score" suffix (e.g., "ifCount" -> "ifScore", "nestingLevels" -> "nestingScore")
        string scoreFieldName = metricField.Replace("Count", "Score").Replace("Levels", "Score");
        FieldInfo? scoreField = metrics.GetType().GetField(scoreFieldName);

        if (scoreField != null && scoreField.FieldType == typeof(double))
        {
            scoreField.SetValue(metrics, score);
        }

        return metrics;
    }

    private static double CalculateLogWeight(double value, double threshold, double scale = 1.0)
    {
        if (value <= threshold)
        {
            return 0.0;
        }

        return Math.Log(1 + (value - threshold) / scale);
    }
}
