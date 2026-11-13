/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using static CognitiveCodeAnalysis.Churn.TreemapMath;

namespace CognitiveCodeAnalysis.Churn;

/// <summary>
/// Renders the churn of all classes as JSON, CSV, HTML, Markdown or an SVG treemap. All numbers use the invariant
/// culture, so the output does not depend on the machine.
/// </summary>
/// <param name="timeProvider">Source of the "generated" timestamp (local time).</param>
/// <param name="coverageProvided">Whether a coverage report was given, so the coverage columns show even when empty.</param>
public sealed class ChurnReportBuilder(TimeProvider timeProvider, bool coverageProvided = false)
{
    private const int SvgWidth = 1200;
    private const int SvgHeight = 800;
    private const int Padding = 2;
    private const int MaxLabelLength = 40;
    private const string NotAvailable = "N/A";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public string Render(ChurnReportType type, IReadOnlyList<ChurnMetrics> metrics) => type switch
    {
        ChurnReportType.Json => Json(metrics),
        ChurnReportType.Csv => Csv(metrics),
        ChurnReportType.Html => Html(metrics),
        ChurnReportType.Markdown => Markdown(metrics),
        ChurnReportType.SvgTreemap => Svg(metrics),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown churn report type"),
    };

    public void Write(ChurnReportType type, string file, IReadOnlyList<ChurnMetrics> metrics) =>
        ChurnReportFileWriter.Write(file, Render(type, metrics));

    private string Timestamp() =>
        timeProvider.GetLocalNow().ToString("yyyy-MM-dd HH:mm:ss", Invariant);

    private bool WithCoverage(IReadOnlyList<ChurnMetrics> metrics) =>
        coverageProvided || metrics.Any(metric => metric.Coverage.HasValue);

    private string Json(IReadOnlyList<ChurnMetrics> metrics)
    {
        using var stream = new MemoryStream();
        var options = new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        using (var writer = new Utf8JsonWriter(stream, options))
        {
            writer.WriteStartObject();
            writer.WriteString("createdAt", Timestamp());
            writer.WriteStartObject("classes");
            foreach (ChurnMetrics metric in metrics)
            {
                writer.WriteStartObject(metric.ClassName);
                writer.WriteString("file", metric.File);
                writer.WriteNumber("score", metric.Score);
                writer.WriteNumber("timesChanged", metric.TimesChanged);
                writer.WriteNumber("churn", metric.Churn);
                WriteNullableNumber(writer, "coverage", metric.Coverage);
                WriteNullableNumber(writer, "riskChurn", metric.RiskChurn);
                if (metric.RiskLevel is { } level)
                {
                    writer.WriteString("riskLevel", Label(level));
                }
                else
                {
                    writer.WriteNull("riskLevel");
                }

                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void WriteNullableNumber(Utf8JsonWriter writer, string name, double? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(name, number);
        }
        else
        {
            writer.WriteNull(name);
        }
    }

    private string Csv(IReadOnlyList<ChurnMetrics> metrics)
    {
        bool withCoverage = WithCoverage(metrics);
        var csv = new StringBuilder();
        var header = new List<string> { "Class", "File", "Score", "Churn", "Times Changed" };
        if (withCoverage)
        {
            header.AddRange(["Risk Churn", "Coverage", "Risk Level"]);
        }

        AppendCsvRecord(csv, header);
        foreach (ChurnMetrics metric in metrics)
        {
            var record = new List<string>
            {
                metric.ClassName, metric.File, Plain(metric.Score), Plain(metric.Churn),
                metric.TimesChanged.ToString(Invariant),
            };
            if (withCoverage)
            {
                record.AddRange([
                    metric.RiskChurn is { } risk ? Plain(risk) : string.Empty,
                    metric.Coverage is { } coverage ? coverage.ToString("R", Invariant) : string.Empty,
                    metric.RiskLevel is { } level ? Label(level) : string.Empty,
                ]);
            }

            AppendCsvRecord(csv, record);
        }

        return csv.ToString();
    }

    private static void AppendCsvRecord(StringBuilder csv, List<string> fields)
    {
        csv.AppendJoin(',', fields.Select(CsvField)).Append('\n');
    }

    private static string CsvField(string value) =>
        value.AsSpan().IndexOfAny(",\"\r\n") >= 0
            ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : value;

    private string Html(IReadOnlyList<ChurnMetrics> metrics)
    {
        bool withCoverage = WithCoverage(metrics);
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n")
            .Append("<meta charset=\"UTF-8\">\n")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n")
            .Append("<title>Churn Metrics Report</title>\n")
            .Append("<link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css\" rel=\"stylesheet\">\n")
            .Append("</head>\n<body>\n<div class=\"container-fluid\">\n")
            .Append("<h1 class=\"mb-4\">Churn Metrics Report - ").Append(Timestamp()).Append("</h1>\n")
            .Append("<p>This report contains the churn metrics for ").Append(metrics.Count.ToString(Invariant))
            .Append(" classes.</p>\n")
            .Append("<table class=\"table table-bordered\">\n<thead>\n<tr>\n");

        var columns = new List<string> { "Class", "Score", "Times Changed", "Churn" };
        if (withCoverage)
        {
            columns.AddRange(["Risk Churn", "Coverage", "Risk Level"]);
        }

        foreach (string column in columns)
        {
            html.Append("<th class=\"table-secondary\">").Append(column).Append("</th>\n");
        }

        html.Append("</tr>\n</thead>\n<tbody>\n");
        foreach (ChurnMetrics metric in metrics)
        {
            html.Append("<tr>\n")
                .Append("<td>").Append(Escape(metric.ClassName)).Append("</td>\n")
                .Append("<td>").Append(Plain(metric.Score)).Append("</td>\n")
                .Append("<td>").Append(metric.TimesChanged.ToString(Invariant)).Append("</td>\n")
                .Append("<td>").Append(Fixed(metric.Churn)).Append("</td>\n");
            if (withCoverage)
            {
                html.Append("<td>").Append(metric.RiskChurn is { } risk ? Fixed(risk) : NotAvailable).Append("</td>\n")
                    .Append("<td>").Append(Percent(metric.Coverage)).Append("</td>\n")
                    .Append("<td>").Append(metric.RiskLevel is { } level ? Label(level) : NotAvailable).Append("</td>\n");
            }

            html.Append("</tr>\n");
        }

        return html.Append("</tbody>\n</table>\n</div>\n</body>\n</html>\n").ToString();
    }

    private string Markdown(IReadOnlyList<ChurnMetrics> metrics)
    {
        bool withCoverage = WithCoverage(metrics);
        var markdown = new StringBuilder()
            .Append("# Churn Metrics Report\n\n")
            .Append("Generated: ").Append(Timestamp()).Append("\n\n")
            .Append("Total Classes: ").Append(metrics.Count.ToString(Invariant)).Append("\n\n")
            .Append(withCoverage
                ? "| Class | Score | Churn | Risk Churn | Times Changed | Coverage | Risk Level |\n"
                  + "| --- | --- | --- | --- | --- | --- | --- |\n"
                : "| Class | Score | Churn | Times Changed |\n| --- | --- | --- | --- |\n");

        foreach (ChurnMetrics metric in metrics.Where(metric => metric.Score != 0 && metric.Churn != 0))
        {
            markdown.Append("| ").Append(metric.ClassName.Replace("|", "\\|", StringComparison.Ordinal))
                .Append(" | ").Append(Plain(metric.Score))
                .Append(" | ").Append(Plain(metric.Churn));
            if (withCoverage)
            {
                markdown.Append(" | ").Append(metric.RiskChurn is { } risk ? Plain(risk) : NotAvailable);
            }

            markdown.Append(" | ").Append(metric.TimesChanged.ToString(Invariant));
            if (withCoverage)
            {
                markdown.Append(" | ").Append(Percent(metric.Coverage))
                    .Append(" | ").Append(metric.RiskLevel is { } level ? Label(level) : NotAvailable);
            }

            markdown.Append(" |\n");
        }

        return markdown.ToString();
    }

    private static string Svg(IReadOnlyList<ChurnMetrics> metrics)
    {
        List<Item> items = PrepareItems(metrics);
        (double min, double max) = FindScoreRange(items);
        string rects = string.Join(
            "\n",
            Layout(items, SvgWidth, SvgHeight)
                .Select(rect => SvgRect(rect, NormalizeScore(rect.Item.Score, min, max))));

        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n"
            + $"<svg width=\"{SvgWidth}\" height=\"{SvgHeight}\" xmlns=\"http://www.w3.org/2000/svg\">\n"
            + "    <style>\n"
            + "        text { pointer-events: none; font-family: Arial, sans-serif; }\n"
            + "        rect:hover { stroke: #000; stroke-width: 2; }\n"
            + "    </style>\n"
            + $"    <rect x=\"0\" y=\"0\" width=\"{SvgWidth}\" height=\"{SvgHeight}\" fill=\"#f8f9fa\"/>\n"
            + "    <g>\n"
            + "        <text x=\"20\" y=\"30\" font-size=\"28\" fill=\"#333\">Churn Treemap</text>\n"
            + "    </g>\n"
            + "    <g>\n"
            + "        " + rects + "\n"
            + "    </g>\n"
            + "</svg>";
    }

    private static string SvgRect(Rect rect, double normalizedScore)
    {
        double x = rect.X + Padding;
        double y = rect.Y + Padding;
        double width = Math.Max(0, rect.Width - (Padding * 2));
        double height = Math.Max(0, rect.Height - (Padding * 2));
        Item item = rect.Item;

        return $"<g><rect x=\"{F2(x)}\" y=\"{F2(y)}\" width=\"{F2(width)}\" height=\"{F2(height)}\""
            + $" fill=\"{ScoreToColor(normalizedScore)}\" stroke=\"#222\" stroke-width=\"1\"/>"
            + $"<title>{Escape(item.ClassName)}&#10;Churn: {Plain(item.Churn)}&#10;Score: {Plain(item.Score)}</title>"
            + $"<text x=\"{F2(x + 4)}\" y=\"{F2(y + 18)}\" font-size=\"13\" fill=\"#000\">{Escape(Label(item.ClassName))}</text></g>";
    }

    /// <summary>The class name cut to 40 characters, the last one being an ellipsis.</summary>
    private static string Label(string className)
    {
        List<Rune> runes = className.EnumerateRunes().ToList();
        return runes.Count > MaxLabelLength
            ? string.Concat(runes.Take(MaxLabelLength - 1).Select(rune => rune.ToString())) + "…"
            : className;
    }

    private static string Label(ChurnRiskLevel level) => level.ToString().ToUpperInvariant();

    /// <summary>The shortest exact form of a value rounded to three decimals: 8.06, and 5 instead of 5.0.</summary>
    private static string Plain(double value) => value.ToString("0.###", Invariant);

    private static string Fixed(double value) => value.ToString("F3", Invariant);

    private static string F2(double value) => value.ToString("F2", Invariant);

    /// <summary>A coverage rate between 0.0 and 1.0 as 83.33%, or N/A for none.</summary>
    private static string Percent(double? rate) =>
        rate is { } value ? (value * 100).ToString("F2", Invariant) + "%" : NotAvailable;

    private static string Escape(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&#39;", StringComparison.Ordinal);
}
