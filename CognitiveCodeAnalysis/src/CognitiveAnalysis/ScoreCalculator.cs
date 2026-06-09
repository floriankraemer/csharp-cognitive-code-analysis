/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Reflection;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class ScoreCalculator
{
    private static readonly Dictionary<string, string> MetricKeyAliases = new(StringComparer.Ordinal)
    {
        { "variableCount", "localVariableCount" },
        { "propertyCallCount", "propertyAccessCount" },
    };

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
    )
    {
        if (!keyValuePair.Value.Enabled)
        {
            return metrics;
        }

        string metricField = ResolveMetricFieldName(keyValuePair.Key);

        // Convert metric key to PascalCase property name (simple conversion)
        string countPropertyName = ToPascalCase(metricField);

        Type metricsType = metrics.GetType();

        // Try property first (PascalCase), otherwise try field (original key, camelCase)
        PropertyInfo? countProperty = metricsType.GetProperty(countPropertyName, BindingFlags.Public | BindingFlags.Instance);
        FieldInfo? countField = metricsType.GetField(metricField, BindingFlags.Public | BindingFlags.Instance);

        if (countProperty == null && countField == null)
        {
            return metrics;
        }

        object? countValue = countProperty != null
            ? countProperty.GetValue(metrics)
            : countField?.GetValue(metrics);

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

        // Set score to property/field with same name but "Score" suffix (e.g., "ifCount" -> "ifScore" or "IfScore")
        string scoreFieldName = GetScoreFieldName(metricField);
        string scorePropertyName = ToPascalCase(scoreFieldName);

        PropertyInfo? scoreProperty = metricsType.GetProperty(scorePropertyName, BindingFlags.Public | BindingFlags.Instance);
        FieldInfo? scoreField = metricsType.GetField(scoreFieldName, BindingFlags.Public | BindingFlags.Instance);

        if (scoreProperty != null && scoreProperty.PropertyType == typeof(double))
        {
            scoreProperty.SetValue(metrics, score);
        }
        else if (scoreField != null && scoreField.FieldType == typeof(double))
        {
            scoreField.SetValue(metrics, score);
        }

        return metrics;
    }

    private static double CalculateLogWeight(
        double value,
        double threshold,
        double scale = 1.0
    )
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

    private static string ResolveMetricFieldName(string configKey)
    {
        return MetricKeyAliases.TryGetValue(configKey, out string? alias)
            ? alias
            : configKey;
    }

    private static string GetScoreFieldName(string countFieldName)
    {
        if (countFieldName == "linesOfCode")
        {
            return "linesOfCodeScore";
        }

        return countFieldName.Replace("Count", "Score").Replace("Levels", "Score");
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        if (name.Length == 1) return name.ToUpperInvariant();

        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
}
