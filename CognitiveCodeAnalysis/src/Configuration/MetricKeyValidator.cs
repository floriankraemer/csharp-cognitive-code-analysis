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
    /// Renames deprecated keys in place and drops every other unknown key, recording a warning each. Nothing fails, so configurations that worked before keep working.
    /// </summary>
    public static IReadOnlyList<string> Normalize(CognitiveConfiguration configuration)
    {
        var warnings = new List<string>();

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
                configuration.Metrics.Remove(key);
                warnings.Add(UnknownKeyMessage(key));
            }
        }

        return warnings;
    }

    public static string UnknownKeyMessage(string key)
        => $"Unknown metric '{key}' in configuration is ignored. Valid metrics: {string.Join(", ", ScoreCalculator.SupportedMetricKeys)}.";
}
