/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

internal static class CognitiveCiSeverity
{
    internal static string SarifLevel(CognitiveMetrics m, CognitiveConfiguration c) =>
        m.totalScore > c.ScoreThreshold ? "warning" : "note";

    internal static string GitlabSeverity(CognitiveMetrics m, CognitiveConfiguration c) =>
        m.totalScore > c.ScoreThreshold ? "minor" : "info";

    internal static string GithubCommandKind(CognitiveMetrics m, CognitiveConfiguration c) =>
        m.totalScore > c.ScoreThreshold ? "warning" : "notice";

    internal static string BuildMessage(
        CognitiveMetrics m,
        CognitiveConfiguration c,
        CognitiveBaselineComparison? baselineComparison = null
    )
    {
        var message =
            $"Cognitive complexity score {m.totalScore:F3} for {m.ClassName}.{m.MethodName} (threshold {c.ScoreThreshold:F3})";

        if (baselineComparison != null
            && baselineComparison.TryGetMethodComparison(m, out MethodMetricsComparison? comparison)
            && comparison != null)
        {
            message += CognitiveReportDeltaFormatter.FormatCiSuffix(comparison.TotalScore, "F3");
        }

        return message;
    }
}
