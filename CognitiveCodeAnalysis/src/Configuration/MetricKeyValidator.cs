/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Configuration;

/// <summary>
/// Checks the keys of the <c>metrics</c> section against the metrics that can be scored.
/// </summary>
internal static class MetricKeyValidator
{
    // The first shipped settings file used these names; they never scored anything.
    private static readonly Dictionary<string, string> DeprecatedAliases = new(StringComparer.Ordinal)
    {
        ["variableCount"] = "localVariableCount",
        ["propertyCallCount"] = "propertyAccessCount",
    };

    /// <summary>
    /// Renames deprecated keys in place (recording a warning each) and rejects every other unknown key.
    /// </summary>
    public static IReadOnlyList<string> NormalizeOrThrow(CognitiveConfiguration configuration)
    {
        var warnings = new List<string>();
        var unknown = new List<string>();

        // Alphabetical, because the configuration providers do not keep the order of the file.
        foreach (string key in configuration.Metrics.Keys.OrderBy(key => key, StringComparer.Ordinal).ToList())
        {
            if (DeprecatedAliases.TryGetValue(key, out string? canonical))
            {
                configuration.Metrics[canonical] = configuration.Metrics[key];
                configuration.Metrics.Remove(key);
                warnings.Add($"Metric key '{key}' is deprecated, use '{canonical}'. It is treated as '{canonical}'.");
            }
            else if (!ScoreCalculator.SupportedMetricKeys.Contains(key))
            {
                unknown.Add(key);
            }
        }

        if (unknown.Count > 0)
        {
            throw new ConfigurationException(UnknownKeyMessage(unknown));
        }

        return warnings;
    }

    public static string UnknownKeyMessage(IReadOnlyList<string> unknownKeys)
    {
        string names = string.Join(", ", unknownKeys.Select(key => $"'{key}'"));
        string noun = unknownKeys.Count == 1 ? "key" : "keys";

        return $"Unknown metric {noun} {names} in cognitive.metrics. "
            + $"Valid keys: {string.Join(", ", ScoreCalculator.SupportedMetricKeys)}.";
    }
}
