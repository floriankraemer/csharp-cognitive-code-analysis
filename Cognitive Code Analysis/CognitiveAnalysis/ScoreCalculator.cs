using System.Reflection;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class ScoreCalculator(CognitiveConfiguration configuration)
{
    public CognitiveMetrics CalculateScores(CognitiveMetrics metrics)
    {
        foreach (KeyValuePair<string, MetricConfiguration> keyValuePair in configuration.Metrics)
        {
            metrics = calculateMetric(metrics, keyValuePair);
        }

        return metrics;
    }

    private static CognitiveMetrics calculateMetric(CognitiveMetrics metrics, KeyValuePair<string, MetricConfiguration> keyValuePair)
    {
        // Skip if metric is not enabled
        if (!keyValuePair.Value.Enabled)
        {
            return metrics;
        }

        string metricField = keyValuePair.Key;

        var countField = metrics.GetType().GetField(metricField);
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
        double score = CalculateLogWeight(count, keyValuePair.Value.Threshold, keyValuePair.Value.Scale);

        // Set score to field with same name but "Score" suffix (e.g., "ifCount" -> "ifScore", "nestingLevels" -> "nestingScore")
        string scoreFieldName = metricField.Replace("Count", "Score").Replace("Levels", "Score");
        FieldInfo? scoreField = metrics.GetType().GetField(scoreFieldName);

        // Only set if the field exists and is of type double (to avoid setting int fields with double values)
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
