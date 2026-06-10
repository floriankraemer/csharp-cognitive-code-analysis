/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Text;

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
        var sb = new StringBuilder();
        sb.Append(string.Format(
            CultureInfo.InvariantCulture,
            "Cognitive complexity score {0:F3} for {1}.{2} (threshold {3:F3}). ",
            m.totalScore,
            m.ClassName,
            m.MethodName,
            c.ScoreThreshold));
        AppendMetricBreakdown(sb, m, c);

        if (baselineComparison != null
            && baselineComparison.TryGetMethodComparison(m, out MethodMetricsComparison? comparison)
            && comparison != null)
        {
            sb.Append(CognitiveReportDeltaFormatter.FormatCiSuffix(comparison.TotalScore, "F3"));
        }

        return sb.ToString();
    }

    private static void AppendMetricBreakdown(StringBuilder sb, CognitiveMetrics m, CognitiveConfiguration c)
    {
        AppendMetricSegment(sb, "lines", m.linesOfCode, m.linesOfCodeScore);
        AppendMetricSegment(sb, "if", m.ifCount, m.ifScore);
        AppendMetricSegment(sb, "else", m.elseCount, m.elseScore);
        AppendMetricSegment(sb, "loop", m.loopCount, m.loopScore);
        AppendMetricSegment(sb, "switch", m.switchCount, m.switchScore);
        AppendMetricSegment(sb, "try-catch", m.tryCatchCount, m.tryCatchScore);
        AppendMetricSegment(sb, "args", m.argumentCount, m.argumentScore);
        AppendMetricSegment(sb, "nesting", m.nestingLevels, m.nestingScore);
        AppendMetricSegment(sb, "returns", m.returnCount, m.returnScore);
        AppendMetricSegment(sb, "locals", m.localVariableCount, m.localVariableScore);
        AppendMetricSegment(sb, "fields", m.fieldAccessCount, m.fieldAccessScore);
        AppendMetricSegment(sb, "props", m.propertyAccessCount, m.propertyAccessScore);

        if (c.ShowHalsteadComplexity && m.Halstead is { } h)
        {
            AppendRawSegment(sb, string.Format(CultureInfo.InvariantCulture, "halstead-vol={0:F2}", h.Volume));
            AppendRawSegment(sb, string.Format(CultureInfo.InvariantCulture, "halstead-diff={0:F2}", h.Difficulty));
            AppendRawSegment(sb, string.Format(CultureInfo.InvariantCulture, "halstead-effort={0:F2}", h.Effort));
        }

        if (c.ShowCyclomaticComplexity)
        {
            AppendRawSegment(sb, string.Format(CultureInfo.InvariantCulture, "cyclomatic={0:F1}", m.cyclomaticComplexity));
        }

        if (m.HasCoverageData)
        {
            if (m.lineCoveragePercentage.HasValue)
            {
                AppendRawSegment(sb, string.Format(CultureInfo.InvariantCulture, "line-coverage={0:F1}%", m.lineCoveragePercentage.Value));
            }

            if (m.branchCoveragePercentage.HasValue)
            {
                AppendRawSegment(sb, string.Format(CultureInfo.InvariantCulture, "branch-coverage={0:F1}%", m.branchCoveragePercentage.Value));
            }

            if (m.churnScore.HasValue)
            {
                AppendRawSegment(sb, string.Format(CultureInfo.InvariantCulture, "churn={0:F3}", m.churnScore.Value));
            }
        }

        if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
        {
            sb.Length--;
        }

        if (sb.Length > 0 && sb[sb.Length - 1] == ';')
        {
            sb.Length--;
        }
    }

    private static void AppendMetricSegment(StringBuilder sb, string name, int count, double score)
    {
        sb.Append(string.Format(CultureInfo.InvariantCulture, "{0}={1}({2:F3}); ", name, count, score));
    }

    private static void AppendRawSegment(StringBuilder sb, string segment)
    {
        sb.Append(segment);
        sb.Append("; ");
    }
}
