/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Configuration;

public class CognitiveConfiguration
{
    public string[] ExcludeFilePatterns { get; set; } = [];
    public string[] ExcludePatterns { get; set; } = [];
    public double ScoreThreshold { get; set; }
    public bool ShowOnlyMethodsExceedingThreshold { get; set; } = true;
    public bool GroupByClass { get; set; }
    public bool CountElseAsNesting { get; set; } = false;
    public bool CountElseIfAsNesting { get; set; } = false;
    public Dictionary<string, MetricConfiguration> Metrics { get; set; } = new();
}
