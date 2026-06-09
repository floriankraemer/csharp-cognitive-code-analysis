/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.HalsteadAnalysis;

/// <summary>
/// Halstead complexity measures for a method (see HalsteadMetricsCalculator for formulas).
/// </summary>
public sealed class HalsteadMetrics
{
    public int DistinctOperators { get; init; }
    public int DistinctOperands { get; init; }
    public int TotalOperators { get; init; }
    public int TotalOperands { get; init; }
    public int ProgramLength { get; init; }
    public int ProgramVocabulary { get; init; }
    public double Volume { get; init; }
    public double Difficulty { get; init; }
    public double Effort { get; init; }
    public string Identifier { get; init; } = "";
}
