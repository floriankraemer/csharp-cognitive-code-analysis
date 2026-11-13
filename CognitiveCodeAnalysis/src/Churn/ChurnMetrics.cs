/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Churn;

/// <summary>How urgently a churning class needs tests, derived from churn and line coverage.</summary>
public enum ChurnRiskLevel
{
    Critical,
    High,
    Medium,
    Low,
}

/// <summary>
/// The git churn of one class: how complex it is (<see cref="Score"/>) times how often its file changed.
/// <see cref="Coverage"/> (0.0 to 1.0), <see cref="RiskChurn"/> and <see cref="RiskLevel"/> are null unless the
/// class has line coverage data.
/// </summary>
public sealed record ChurnMetrics(
    string ClassName,
    string File,
    double Score,
    int TimesChanged,
    double Churn,
    double? Coverage = null,
    double? RiskChurn = null,
    ChurnRiskLevel? RiskLevel = null
);
