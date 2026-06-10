/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Text;

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

    internal static string BuildMessage(CognitiveMetrics m, CognitiveConfiguration c)
    {
        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"Cognitive complexity score {m.totalScore:F3} for {m.ClassName}.{m.MethodName} (threshold {c.ScoreThreshold:F3}). ");
        AppendMetricBreakdown(sb, m, c);
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
            AppendRawSegment(sb, $"halstead-vol={h.Volume:F2}");
            AppendRawSegment(sb, $"halstead-diff={h.Difficulty:F2}");
            AppendRawSegment(sb, $"halstead-effort={h.Effort:F2}");
        }

        if (c.ShowCyclomaticComplexity)
        {
            AppendRawSegment(sb, $"cyclomatic={m.cyclomaticComplexity:F1}");
        }

        if (m.HasCoverageData)
        {
            if (m.lineCoveragePercentage.HasValue)
            {
                AppendRawSegment(sb, $"line-coverage={m.lineCoveragePercentage.Value:F1}%");
            }

            if (m.branchCoveragePercentage.HasValue)
            {
                AppendRawSegment(sb, $"branch-coverage={m.branchCoveragePercentage.Value:F1}%");
            }

            if (m.churnScore.HasValue)
            {
                AppendRawSegment(sb, $"churn={m.churnScore.Value:F3}");
            }
        }

        if (sb.Length > 0 && sb[^1] == ' ')
        {
            sb.Length--;
        }

        if (sb.Length > 0 && sb[^1] == ';')
        {
            sb.Length--;
        }
    }

    private static void AppendMetricSegment(StringBuilder sb, string name, int count, double score)
    {
        sb.Append(CultureInfo.InvariantCulture, $"{name}={count}({score:F3}); ");
    }

    private static void AppendRawSegment(StringBuilder sb, string segment)
    {
        sb.Append(segment);
        sb.Append("; ");
    }
}
