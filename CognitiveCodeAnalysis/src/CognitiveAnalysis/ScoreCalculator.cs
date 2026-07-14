/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Reflection;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class ScoreCalculator
{
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
        if (!keyValuePair.Value.Enabled)
        {
            return metrics;
        }

        string metricField = keyValuePair.Key;
        double count = GetCountValue(metrics, metricField);
        double score = CalculateLogWeight(
            value: count,
            threshold: keyValuePair.Value.Threshold,
            scale: keyValuePair.Value.Scale
        );
        SetScoreValue(metrics, metricField, score);

        return metrics;
    }

    private static double GetCountValue(CognitiveMetrics metrics, string metricField) => metricField switch
    {
        "ifCount" => metrics.ifCount,
        "elseCount" => metrics.elseCount,
        "loopCount" => metrics.loopCount,
        "switchCount" => metrics.switchCount,
        "tryCatchCount" => metrics.tryCatchCount,
        "returnCount" => metrics.returnCount,
        "argumentCount" => metrics.argumentCount,
        "nestingLevels" => metrics.nestingLevels,
        "linesOfCode" => metrics.linesOfCode,
        "localVariableCount" => metrics.localVariableCount,
        "fieldAccessCount" => metrics.fieldAccessCount,
        "propertyAccessCount" => metrics.propertyAccessCount,
        "cyclomaticComplexity" => metrics.cyclomaticComplexity,
        _ => GetCountViaReflection(metrics, metricField)
    };

    private static void SetScoreValue(CognitiveMetrics metrics, string metricField, double score)
    {
        switch (metricField)
        {
            case "ifCount": metrics.ifScore = score; break;
            case "elseCount": metrics.elseScore = score; break;
            case "loopCount": metrics.loopScore = score; break;
            case "switchCount": metrics.switchScore = score; break;
            case "tryCatchCount": metrics.tryCatchScore = score; break;
            case "returnCount": metrics.returnScore = score; break;
            case "argumentCount": metrics.argumentScore = score; break;
            case "nestingLevels": metrics.nestingScore = score; break;
            case "linesOfCode": metrics.linesOfCodeScore = score; break;
            case "localVariableCount": metrics.localVariableScore = score; break;
            case "fieldAccessCount": metrics.fieldAccessScore = score; break;
            case "propertyAccessCount": metrics.propertyAccessScore = score; break;
            default: SetScoreViaReflection(metrics, metricField, score); break;
        }
    }

    private static double GetCountViaReflection(CognitiveMetrics metrics, string metricField)
    {
        string countPropertyName = ToPascalCase(metricField);
        Type metricsType = metrics.GetType();

        PropertyInfo? countProperty = metricsType.GetProperty(countPropertyName, BindingFlags.Public | BindingFlags.Instance);
        FieldInfo? countField = metricsType.GetField(metricField, BindingFlags.Public | BindingFlags.Instance);

        if (countProperty == null && countField == null)
        {
            return 0.0;
        }

        object? countValue = countProperty != null
            ? countProperty.GetValue(metrics)
            : countField?.GetValue(metrics);

        return countValue == null ? 0.0 : Convert.ToDouble(countValue);
    }

    private static void SetScoreViaReflection(CognitiveMetrics metrics, string metricField, double score)
    {
        string scoreFieldName = GetScoreFieldName(metricField);
        string scorePropertyName = ToPascalCase(scoreFieldName);
        Type metricsType = metrics.GetType();

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
