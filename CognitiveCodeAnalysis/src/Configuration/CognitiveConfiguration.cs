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
    public bool ShowHalsteadComplexity { get; set; }
    public bool ShowCyclomaticComplexity { get; set; }
    public bool ShowCouplingMetrics { get; set; }

    /// <summary>
    /// Shows the understandability (SonarSource cognitive complexity) column in the console, HTML and Markdown reports
    /// and its fields in the CSV report.
    /// </summary>
    public bool ShowUnderstandability { get; set; }

    /// <summary>
    /// Shows the detail columns (lines, arguments, returns, nesting, ...) in the console, HTML and Markdown reports.
    /// </summary>
    public bool ShowDetailedCognitiveMetrics { get; set; } = true;
    public Dictionary<string, MetricConfiguration> Metrics { get; set; } = new();

    /// <summary>
    /// Notes collected while loading the configuration, such as deprecated metric keys that were accepted.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; internal set; } = [];
}
