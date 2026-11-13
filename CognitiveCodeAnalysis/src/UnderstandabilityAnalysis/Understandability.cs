/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

namespace CognitiveCodeAnalysis.UnderstandabilityAnalysis;

/// <summary>
/// The understandability of one method: its cognitive complexity as specified by SonarSource.
/// </summary>
/// <param name="Structural">The number of structural increments.</param>
/// <param name="Hybrid">The number of hybrid increments (<c>else</c>, <c>else if</c>).</param>
/// <param name="Fundamental">The number of fundamental increments, including the recursion.</param>
/// <param name="Nesting">The points added for nesting.</param>
/// <param name="Recursion">1 when the method calls itself, else 0; also counted in <paramref name="Fundamental"/>.</param>
public sealed record Understandability(int Structural, int Hybrid, int Fundamental, int Nesting, int Recursion)
{
    public static readonly Understandability None = new(0, 0, 0, 0, 0);

    public int Total => Structural + Hybrid + Fundamental + Nesting;

    public string RiskLevel => UnderstandabilityAnalysis.RiskLevel.Of(Total);

    /// <summary>The value as shown in the report tables, such as <c>7 (medium)</c>.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Total} ({RiskLevel})");
}
