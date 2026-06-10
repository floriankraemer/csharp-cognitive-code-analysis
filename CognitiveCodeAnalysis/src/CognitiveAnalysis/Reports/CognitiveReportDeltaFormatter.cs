/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public static class CognitiveReportDeltaFormatter
{
    public static string FormatHtmlValue(string formattedValue, MetricDelta? delta, string deltaFormat)
    {
        string suffix = FormatHtmlDeltaSuffix(delta, deltaFormat);
        return string.IsNullOrEmpty(suffix) ? formattedValue : formattedValue + " " + suffix;
    }

    public static string FormatHtmlDeltaSuffix(MetricDelta? delta, string format)
    {
        if (delta?.Delta is not { } value || value == 0)
        {
            return string.Empty;
        }

        string formatted = Math.Abs(value).ToString(format, CultureInfo.InvariantCulture);
        return value > 0
            ? $"<span class=\"delta-up\">▲{formatted}</span>"
            : $"<span class=\"delta-down\">▼{formatted}</span>";
    }

    public static string FormatConsoleValue(string formattedValue, MetricDelta? delta, string deltaFormat)
    {
        string suffix = FormatConsoleDeltaSuffix(delta, deltaFormat);
        return string.IsNullOrEmpty(suffix) ? formattedValue : formattedValue + " " + suffix;
    }

    public static string FormatConsoleDeltaSuffix(MetricDelta? delta, string format)
    {
        if (delta?.Delta is not { } value || value == 0)
        {
            return string.Empty;
        }

        string formatted = Math.Abs(value).ToString(format, CultureInfo.InvariantCulture);
        return value > 0
            ? $"[red]▲{formatted}[/]"
            : $"[green]▼{formatted}[/]";
    }

    public static string FormatCsvDelta(MetricDelta? delta, string format)
    {
        if (delta?.Delta is not { } value)
        {
            return string.Empty;
        }

        return value.ToString(format, CultureInfo.InvariantCulture);
    }

    public static string FormatCiSuffix(MetricDelta? delta, string format)
    {
        if (delta?.Delta is not { } value || value == 0)
        {
            return string.Empty;
        }

        string formatted = Math.Abs(value).ToString(format, CultureInfo.InvariantCulture);
        string symbol = value > 0 ? "▲" : "▼";
        return $" ({symbol}{formatted} vs baseline)";
    }

    public static string FormatCountWithScoreHtml(int count, double score, MetricDelta? countDelta, MetricDelta? scoreDelta)
    {
        string countText = count.ToString(CultureInfo.InvariantCulture);
        string scoreText = score.ToString("F3", CultureInfo.InvariantCulture);
        return FormatHtmlValue(countText, countDelta, "F0")
            + " ("
            + FormatHtmlValue(scoreText, scoreDelta, "F3")
            + ")";
    }

    public static string FormatCountWithScoreConsole(int count, double score, MetricDelta? countDelta, MetricDelta? scoreDelta)
    {
        string countText = count.ToString(CultureInfo.InvariantCulture);
        string scoreText = score.ToString("F3", CultureInfo.InvariantCulture);
        return FormatConsoleValue(countText, countDelta, "F0")
            + " ("
            + FormatConsoleValue(scoreText, scoreDelta, "F3")
            + ")";
    }
}
