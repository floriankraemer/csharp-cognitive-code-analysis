/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Configuration;

public static class CognitiveConfigurationDefaults
{
    public static CognitiveConfiguration Create() =>
        new()
        {
            ExcludeFilePatterns = [],
            ExcludePatterns = [],
            ScoreThreshold = 0.5,
            ShowOnlyMethodsExceedingThreshold = true,
            ShowHalsteadComplexity = false,
            ShowCyclomaticComplexity = false,
            ShowCouplingMetrics = false,
            GroupByClass = true,
            CountElseAsNesting = false,
            CountElseIfAsNesting = false,
            Metrics = CreateDefaultMetrics(),
        };

    private static Dictionary<string, MetricConfiguration> CreateDefaultMetrics() =>
        new(StringComparer.Ordinal)
        {
            ["linesOfCode"] = new MetricConfiguration { Threshold = 60, Scale = 25.0, Enabled = true },
            ["argumentCount"] = new MetricConfiguration { Threshold = 4, Scale = 1.0, Enabled = true },
            ["returnCount"] = new MetricConfiguration { Threshold = 2, Scale = 5.0, Enabled = true },
            ["variableCount"] = new MetricConfiguration { Threshold = 4, Scale = 5.0, Enabled = false },
            ["propertyCallCount"] = new MetricConfiguration { Threshold = 4, Scale = 15.0, Enabled = false },
            ["fieldAccessCount"] = new MetricConfiguration { Threshold = 4, Scale = 15.0, Enabled = false },
            ["ifCount"] = new MetricConfiguration { Threshold = 3, Scale = 1.0, Enabled = true },
            ["nestingLevels"] = new MetricConfiguration { Threshold = 1, Scale = 1.0, Enabled = true },
            ["elseCount"] = new MetricConfiguration { Threshold = 1, Scale = 1.0, Enabled = true },
            ["loopCount"] = new MetricConfiguration { Threshold = 2, Scale = 1.0, Enabled = false },
            ["switchCount"] = new MetricConfiguration { Threshold = 1, Scale = 1.0, Enabled = false },
            ["tryCatchCount"] = new MetricConfiguration { Threshold = 1, Scale = 1.0, Enabled = false },
        };
}
