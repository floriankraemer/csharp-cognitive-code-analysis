/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

public sealed class ClassCouplingComparison
{
    public required ClassCouplingMetrics Current { get; init; }

    public CognitiveBaselineClassCouplingSnapshot? Baseline { get; init; }

    public bool HasBaseline => Baseline != null;

    public MetricDelta IncomingCoupling { get; init; } = new();

    public MetricDelta OutgoingCoupling { get; init; } = new();

    public MetricDelta Stability { get; init; } = new();
}
