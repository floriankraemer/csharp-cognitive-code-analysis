/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

internal static class CognitiveReportTableFormat
{
    internal static IReadOnlyList<string> BuildColumnHeaders(
        CognitiveConfiguration configuration,
        bool hasCoverageData
    )
    {
        var headers = new List<string>
        {
            "Method",
            "Score",
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
        };

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
        bool hasCoverageData
    )
    {
        var row = new List<string>
        {
            $"L{metrics.methodLineNumber} {metrics.MethodName}",
            FormatInvariant(metrics.totalScore, "F3"),
            metrics.linesOfCode.ToString(CultureInfo.InvariantCulture),
            FormatCountWithScore(metrics.ifCount, metrics.ifScore),
            FormatCountWithScore(metrics.elseCount, metrics.elseScore),
            FormatCountWithScore(metrics.loopCount, metrics.loopScore),
            FormatCountWithScore(metrics.switchCount, metrics.switchScore),
            FormatCountWithScore(metrics.tryCatchCount, metrics.tryCatchScore),
            FormatCountWithScore(metrics.argumentCount, metrics.argumentScore),
            FormatCountWithScore(metrics.nestingLevels, metrics.nestingScore),
            FormatCountWithScore(metrics.returnCount, metrics.returnScore),
            FormatCountWithScore(metrics.localVariableCount, metrics.localVariableScore),
            FormatCountWithScore(metrics.fieldAccessCount, metrics.fieldAccessScore),
            FormatCountWithScore(metrics.propertyAccessCount, metrics.propertyAccessScore),
        };

        if (configuration.ShowHalsteadComplexity)
        {
            row.Add(FormatHalsteadDouble(metrics.Halstead?.Volume));
            row.Add(FormatHalsteadDouble(metrics.Halstead?.Difficulty));
            row.Add(FormatHalsteadDouble(metrics.Halstead?.Effort));
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            row.Add(FormatInvariant(metrics.cyclomaticComplexity, "F1"));
        }

        if (hasCoverageData)
        {
            row.Add(
                metrics.churnScore.HasValue
                    ? FormatInvariant(metrics.churnScore.Value, "F3")
                    : "n/a"
            );
        }

        return row;
    }

    internal static string FormatCountWithScore(int count, double score)
        => $"{count} ({FormatInvariant(score, "F3")})";

    internal static string FormatInvariant(double value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture);

    internal static string FormatHalsteadDouble(double? value)
        => value.HasValue ? value.Value.ToString("F2", CultureInfo.InvariantCulture) : "n/a";

    internal static string EscapeMarkdownTableCell(string value)
        => value.Replace("|", "\\|");
}
