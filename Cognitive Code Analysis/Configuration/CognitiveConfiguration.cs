namespace CognitiveCodeAnalysis.Configuration;

public class CognitiveConfiguration
{
    public string[] ExcludeFilePatterns { get; set; } = Array.Empty<string>();
    public string[] ExcludePatterns { get; set; } = Array.Empty<string>();
    public double ScoreThreshold { get; set; }
    public bool ShowOnlyMethodsExceedingThreshold { get; set; }
    public bool ShowHalsteadComplexity { get; set; }
    public bool ShowCyclomaticComplexity { get; set; }
    public bool ShowDetailedCognitiveMetrics { get; set; }
    public bool GroupByClass { get; set; }
    public bool CountElseAsNesting { get; set; } = false;
    public bool CountElseIfAsNesting { get; set; } = false;
    public Dictionary<string, MetricConfiguration> Metrics { get; set; } = new();
}
