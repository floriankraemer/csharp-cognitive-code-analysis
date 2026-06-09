/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CouplingAnalysis;

public sealed class ClassCouplingMetrics
{
    public required string ClassName { get; init; }
    public int IncomingCoupling { get; init; }
    public int OutgoingCoupling { get; init; }
    public double Stability { get; init; }
}
