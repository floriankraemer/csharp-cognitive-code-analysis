/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

/// <summary>Identifies the metric settings a baseline was scored with, the only part of the configuration that changes scores.</summary>
public static class BaselineConfigHash
{
    private static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>The SHA-256, as lower-case hex, of the metrics sorted by name and written compactly.</summary>
    public static string Of(CognitiveConfiguration configuration)
    {
        var sorted = new SortedDictionary<string, MetricConfiguration>(configuration.Metrics, StringComparer.Ordinal);
        string canonical = JsonSerializer.Serialize(sorted, CanonicalOptions);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    /// <summary>The warning when the baseline was scored with other settings; null when they match or the baseline has no hash.</summary>
    public static string? Warning(string? baselineHash, string currentHash) =>
        baselineHash == null || string.Equals(baselineHash, currentHash, StringComparison.Ordinal)
            ? null
            : $"Baseline config hash ({baselineHash}) does not match current config hash ({currentHash}). "
              + "Metrics comparison may not be accurate.";
}
