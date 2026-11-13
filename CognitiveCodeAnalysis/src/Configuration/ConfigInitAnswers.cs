/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Configuration;

/// <summary>
/// The settings the <c>init</c> command asks for. Every other setting keeps the built-in default.
/// </summary>
public sealed record ConfigInitAnswers(
    double ScoreThreshold,
    bool ShowOnlyMethodsExceedingThreshold,
    bool ShowHalsteadComplexity,
    bool ShowCyclomaticComplexity,
    bool ShowCouplingMetrics,
    bool ShowDetailedCognitiveMetrics,
    bool GroupByClass
)
{
    public static ConfigInitAnswers FromConfiguration(CognitiveConfiguration configuration) => new(
        configuration.ScoreThreshold,
        configuration.ShowOnlyMethodsExceedingThreshold,
        configuration.ShowHalsteadComplexity,
        configuration.ShowCyclomaticComplexity,
        configuration.ShowCouplingMetrics,
        configuration.ShowDetailedCognitiveMetrics,
        configuration.GroupByClass
    );
}
