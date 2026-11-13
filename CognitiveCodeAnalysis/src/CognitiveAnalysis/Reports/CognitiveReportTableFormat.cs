/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

internal static class CognitiveReportTableFormat
{
    internal static IReadOnlyList<string> BuildColumnHeaders(
        CognitiveConfiguration configuration,
        bool hasCoverageData
    )
    {
        var headers = new List<string> { "Method", "Score" };

        if (configuration.ShowDetailedCognitiveMetrics)
        {
            headers.AddRange(
            [
                "Lines",
                "Ifs",
                "Else",
                "Loops",
                "Switch",
                "Try/Catch",
                "Arguments",
                "Nesting",
                "Returns",
                "Locals",
                "Fields",
                "Props",
            ]);
        }

        if (configuration.ShowHalsteadComplexity)
        {
            headers.Add("Halstead Volume");
            headers.Add("Halstead Difficulty");
            headers.Add("Halstead Effort");
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            headers.Add("Cyclomatic Complexity");
        }

        if (hasCoverageData)
        {
            headers.Add("Churn");
        }

        return headers;
    }

    internal static IReadOnlyList<string> BuildRowValues(
        CognitiveMetrics metrics,
        CognitiveConfiguration configuration,
        bool hasCoverageData,
        MethodMetricsComparison? comparison = null
    )
    {
        var row = new List<string>
        {
            $"L{metrics.methodLineNumber} {metrics.MethodName}",
            DeltaValue(metrics.totalScore, comparison?.TotalScore, "F3"),
        };

        if (configuration.ShowDetailedCognitiveMetrics)
        {
            row.AddRange(
            [
                CognitiveReportDeltaFormatter.FormatMarkdownValue(metrics.linesOfCode.ToString(CultureInfo.InvariantCulture), comparison?.LinesOfCode, "F0"),
                CognitiveReportDeltaFormatter.FormatCountWithScoreMarkdown(metrics.ifCount, metrics.ifScore, comparison?.IfCount, comparison?.IfScore),
                CognitiveReportDeltaFormatter.FormatCountWithScoreMarkdown(metrics.elseCount, metrics.elseScore, comparison?.ElseCount, comparison?.ElseScore),
                CognitiveReportDeltaFormatter.FormatCountWithScoreMarkdown(metrics.loopCount, metrics.loopScore, comparison?.LoopCount, comparison?.LoopScore),
                CognitiveReportDeltaFormatter.FormatCountWithScoreMarkdown(metrics.switchCount, metrics.switchScore, comparison?.SwitchCount, comparison?.SwitchScore),
                CognitiveReportDeltaFormatter.FormatCountWithScoreMarkdown(metrics.tryCatchCount, metrics.tryCatchScore, comparison?.TryCatchCount, comparison?.TryCatchScore),
                CognitiveReportDeltaFormatter.FormatCountWithScoreMarkdown(metrics.argumentCount, metrics.argumentScore, comparison?.ArgumentCount, comparison?.ArgumentScore),
                CognitiveReportDeltaFormatter.FormatCountWithScoreMarkdown(metrics.nestingLevels, metrics.nestingScore, comparison?.NestingLevels, comparison?.NestingScore),
                CognitiveReportDeltaFormatter.FormatCountWithScoreMarkdown(metrics.returnCount, metrics.returnScore, comparison?.ReturnCount, comparison?.ReturnScore),
                CognitiveReportDeltaFormatter.FormatCountWithScoreMarkdown(metrics.localVariableCount, metrics.localVariableScore, comparison?.LocalVariableCount, comparison?.LocalVariableScore),
                CognitiveReportDeltaFormatter.FormatCountWithScoreMarkdown(metrics.fieldAccessCount, metrics.fieldAccessScore, comparison?.FieldAccessCount, comparison?.FieldAccessScore),
                CognitiveReportDeltaFormatter.FormatCountWithScoreMarkdown(metrics.propertyAccessCount, metrics.propertyAccessScore, comparison?.PropertyAccessCount, comparison?.PropertyAccessScore),
            ]);
        }

        if (configuration.ShowHalsteadComplexity)
        {
            row.Add(HalsteadValue(metrics.Halstead?.Volume, comparison?.HalsteadVolume));
            row.Add(HalsteadValue(metrics.Halstead?.Difficulty, comparison?.HalsteadDifficulty));
            row.Add(HalsteadValue(metrics.Halstead?.Effort, comparison?.HalsteadEffort));
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            row.Add(DeltaValue(metrics.cyclomaticComplexity, comparison?.CyclomaticComplexity, "F1"));
        }

        if (hasCoverageData)
        {
            row.Add(
                metrics.churnScore.HasValue
                    ? DeltaValue(metrics.churnScore.Value, comparison?.ChurnScore, "F3")
                    : "n/a"
            );
        }

        return row;
    }

    private static string DeltaValue(double value, MetricDelta? delta, string format)
        => CognitiveReportDeltaFormatter.FormatMarkdownValue(FormatInvariant(value, format), delta, format);

    private static string HalsteadValue(double? value, MetricDelta? delta)
        => value.HasValue ? DeltaValue(value.Value, delta, "F2") : "n/a";

    internal static string FormatCountWithScore(int count, double score)
        => $"{count} ({FormatInvariant(score, "F3")})";

    internal static string FormatInvariant(double value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture);

    internal static string FormatHalsteadDouble(double? value)
        => value.HasValue ? value.Value.ToString("F2", CultureInfo.InvariantCulture) : "n/a";

    internal static string EscapeMarkdownTableCell(string value)
        => value.Replace("|", "\\|");
}
