/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

public sealed class MethodMetricsComparison
{
    public required CognitiveMetrics Current { get; init; }

    public CognitiveBaselineMethodSnapshot? Baseline { get; init; }

    public bool HasBaseline => Baseline != null;

    public MetricDelta TotalScore { get; init; } = new();

    public MetricDelta LinesOfCode { get; init; } = new();

    public MetricDelta LinesOfCodeScore { get; init; } = new();

    public MetricDelta IfCount { get; init; } = new();

    public MetricDelta IfScore { get; init; } = new();

    public MetricDelta ElseCount { get; init; } = new();

    public MetricDelta ElseScore { get; init; } = new();

    public MetricDelta LoopCount { get; init; } = new();

    public MetricDelta LoopScore { get; init; } = new();

    public MetricDelta SwitchCount { get; init; } = new();

    public MetricDelta SwitchScore { get; init; } = new();

    public MetricDelta TryCatchCount { get; init; } = new();

    public MetricDelta TryCatchScore { get; init; } = new();

    public MetricDelta ReturnCount { get; init; } = new();

    public MetricDelta ReturnScore { get; init; } = new();

    public MetricDelta ArgumentCount { get; init; } = new();

    public MetricDelta ArgumentScore { get; init; } = new();

    public MetricDelta NestingLevels { get; init; } = new();

    public MetricDelta NestingScore { get; init; } = new();

    public MetricDelta LocalVariableCount { get; init; } = new();

    public MetricDelta LocalVariableScore { get; init; } = new();

    public MetricDelta FieldAccessCount { get; init; } = new();

    public MetricDelta FieldAccessScore { get; init; } = new();

    public MetricDelta PropertyAccessCount { get; init; } = new();

    public MetricDelta PropertyAccessScore { get; init; } = new();

    public MetricDelta CyclomaticComplexity { get; init; } = new();

    public MetricDelta HalsteadVolume { get; init; } = new();

    public MetricDelta HalsteadDifficulty { get; init; } = new();

    public MetricDelta HalsteadEffort { get; init; } = new();

    public MetricDelta LineCoveragePercentage { get; init; } = new();

    public MetricDelta BranchCoveragePercentage { get; init; } = new();

    public MetricDelta ChurnScore { get; init; } = new();
}
