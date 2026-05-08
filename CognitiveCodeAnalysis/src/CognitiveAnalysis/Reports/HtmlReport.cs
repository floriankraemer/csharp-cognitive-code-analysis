/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text;

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public class HtmlReport() : IReport
{
    public string Name => "Html";

    public void RenderMetrics(
        string outputFile,
        CognitiveMetricsCollection metricsCollection,
        CognitiveConfiguration configuration
    ) {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"en\">");
        html.AppendLine("<head>");
        html.AppendLine("    <meta charset=\"UTF-8\">");
        html.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine("    <title>Cognitive Code Analysis Report</title>");
        html.AppendLine("    <link href=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css\" rel=\"stylesheet\">");
        html.AppendLine("    <style>");
        html.AppendLine("        body { font-size: 0.9rem; }");
        html.AppendLine("        .table { font-size: 0.9rem; }");
        html.AppendLine("        .score-green { color: #28a745; font-weight: bold; }");
        html.AppendLine("        .score-yellow { color: #ffc107; font-weight: bold; }");
        html.AppendLine("        .score-red { color: #dc3545; font-weight: bold; }");
        html.AppendLine("        .class-header { margin-top: 2rem; margin-bottom: 1rem; }");
        html.AppendLine("        .class-header:first-child { margin-top: 1rem; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"container-fluid mt-4\">");
        html.AppendLine("        <h1 class=\"mb-4\">Cognitive Code Analysis Report</h1>");

        HandleGrouping(metricsCollection, html, configuration);

        html.AppendLine("    </div>");
        html.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js\"></script>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        WriteReportToFile(outputFile, html);
    }

    private void HandleGrouping(CognitiveMetricsCollection metricsCollection, StringBuilder html, CognitiveConfiguration configuration)
    {
        bool hasCoverageData = metricsCollection.HasCoverageData();

        if (configuration.GroupByClass)
        {
            html.Append(RenderMetricsGrouped(metricsCollection));
            return;
        }

        html.Append(RenderMetricsUngrouped(metricsCollection));
    }

    private void WriteReportToFile(string outputFilePath, StringBuilder html)
    {
        // Ensure directory exists
        string? directory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputFilePath, html.ToString());
    }

    private string RenderMetricsGrouped(CognitiveMetricsCollection metricsCollection)
    {
        var html = new StringBuilder();
        bool hasCoverageData = metricsCollection.HasCoverageData();
        var groupedByClass = metricsCollection
            .GroupBy(m => new { m.ClassName, m.FilePath })
            .OrderBy(g => g.Key.ClassName);

        foreach (var classGroup in groupedByClass)
        {
            var classMetrics = classGroup.ToList();
            var firstMetric = classMetrics.First();

            html.AppendLine("        <div class=\"class-header\">");
            html.AppendLine($"            <h3 class=\"text-primary\">Class: {HtmlEncode(firstMetric.ClassName)}</h3>");
            html.AppendLine($"            <p class=\"text-muted\">File: {HtmlEncode(firstMetric.FilePath)}</p>");
            html.AppendLine("        </div>");

            html.AppendLine("        <div class=\"table-responsive\">");
            html.AppendLine("            <table class=\"table table-striped table-bordered\">");
            html.AppendLine("                <thead class=\"table-dark\">");
            html.AppendLine("                    <tr>");
            html.AppendLine("                        <th>Method</th>");
            html.AppendLine("                        <th>Score</th>");
            html.AppendLine("                        <th>Lines</th>");
            html.AppendLine("                        <th>Ifs</th>");
            html.AppendLine("                        <th>Arguments</th>");
            html.AppendLine("                        <th>Nesting</th>");
            html.AppendLine("                        <th>Returns</th>");
            if (hasCoverageData)
            {
                html.AppendLine("                        <th>Churn</th>");
            }
            html.AppendLine("                    </tr>");
            html.AppendLine("                </thead>");
            html.AppendLine("                <tbody>");

            foreach (var metrics in classMetrics)
            {
                html.AppendLine("                    <tr>");
                html.AppendLine($"                        <td>L{metrics.methodLineNumber} {HtmlEncode(metrics.MethodName)}</td>");
                html.AppendLine($"                        <td><span class=\"{GetScoreClass(metrics.totalScore)}\">{metrics.totalScore:F3}</span></td>");
                html.AppendLine($"                        <td>{metrics.linesOfCode}</td>");
                html.AppendLine($"                        <td>{metrics.ifCount} ({metrics.ifScore:F3})</td>");
                html.AppendLine($"                        <td>{metrics.argumentCount} ({metrics.argumentScore:F3})</td>");
                html.AppendLine($"                        <td>{metrics.nestingLevels} ({metrics.nestingScore:F3})</td>");
                html.AppendLine($"                        <td>{metrics.returnCount} ({metrics.returnScore:F3})</td>");
                if (hasCoverageData)
                {
                    string churnValue = metrics.churnScore.HasValue 
                        ? metrics.churnScore.Value.ToString("F3") 
                        : "n/a";
                    double churnScoreForColor = metrics.churnScore ?? 0;
                    html.AppendLine($"                        <td><span class=\"{GetChurnScoreClass(churnScoreForColor)}\">{churnValue}</span></td>");
                }
                html.AppendLine("                    </tr>");
            }

            html.AppendLine("                </tbody>");
            html.AppendLine("            </table>");
            html.AppendLine("        </div>");
        }

        return html.ToString();
    }

    private string RenderMetricsUngrouped(CognitiveMetricsCollection metricsCollection)
    {
        var html = new StringBuilder();
        bool hasCoverageData = metricsCollection.HasCoverageData();

        foreach (var metrics in metricsCollection)
        {
            html.AppendLine("        <div class=\"class-header\">");
            html.AppendLine($"            <h3 class=\"text-primary\">Class: {HtmlEncode(metrics.ClassName)}</h3>");
            html.AppendLine($"            <p class=\"text-success\">Method: {HtmlEncode(metrics.methodSignature)}</p>");
            html.AppendLine($"            <p class=\"text-muted\">File: {HtmlEncode(metrics.FilePath)}</p>");
            html.AppendLine("        </div>");

            html.AppendLine("        <div class=\"table-responsive\">");
            html.AppendLine("            <table class=\"table table-striped table-bordered\">");
            html.AppendLine("                <thead class=\"table-dark\">");
            html.AppendLine("                    <tr>");
            html.AppendLine("                        <th>Method</th>");
            html.AppendLine("                        <th>Score</th>");
            html.AppendLine("                        <th>Lines</th>");
            html.AppendLine("                        <th>Ifs</th>");
            html.AppendLine("                        <th>Arguments</th>");
            html.AppendLine("                        <th>Nesting</th>");
            html.AppendLine("                        <th>Returns</th>");
            if (hasCoverageData)
            {
                html.AppendLine("                        <th>Churn</th>");
            }
            html.AppendLine("                    </tr>");
            html.AppendLine("                </thead>");
            html.AppendLine("                <tbody>");
            html.AppendLine("                    <tr>");
            html.AppendLine($"                        <td>L{metrics.methodLineNumber} {HtmlEncode(metrics.MethodName)}</td>");
            html.AppendLine($"                        <td><span class=\"{GetScoreClass(metrics.totalScore)}\">{metrics.totalScore:F3}</span></td>");
            html.AppendLine($"                        <td>{metrics.linesOfCode}</td>");
            html.AppendLine($"                        <td>{metrics.ifCount} ({metrics.ifScore:F3})</td>");
            html.AppendLine($"                        <td>{metrics.argumentCount} ({metrics.argumentScore:F3})</td>");
            html.AppendLine($"                        <td>{metrics.nestingLevels} ({metrics.nestingScore:F3})</td>");
            html.AppendLine($"                        <td>{metrics.returnCount} ({metrics.returnScore:F3})</td>");
            if (hasCoverageData)
            {
                string churnValue = metrics.churnScore.HasValue 
                    ? metrics.churnScore.Value.ToString("F3") 
                    : "n/a";
                double churnScoreForColor = metrics.churnScore ?? 0;
                html.AppendLine($"                        <td><span class=\"{GetChurnScoreClass(churnScoreForColor)}\">{churnValue}</span></td>");
            }
            html.AppendLine("                    </tr>");
            html.AppendLine("                </tbody>");
            html.AppendLine("            </table>");
            html.AppendLine("        </div>");
        }

        return html.ToString();
    }

    private static string GetScoreClass(double score)
    {
        return score switch
        {
            < 0.5 => "score-green",
            < 0.85 => "score-yellow",
            _ => "score-red",
        };
    }

    /// <summary>
    /// <![CDATA[
    /// Gets the CSS class for churn score based on risk thresholds.
    /// Green < 0.3 (low risk), Yellow 0.3-0.7 (medium risk), Red > 0.7 (high risk)
    /// ]]>
    /// </summary>
    /// <param name="churnScore">The churn score</param>
    /// <returns>CSS class name for styling</returns>
    private static string GetChurnScoreClass(double churnScore)
    {
        return churnScore switch
        {
            < 0.3 => "score-green",
            <= 0.7 => "score-yellow",
            _ => "score-red",
        };
    }

    private static string HtmlEncode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return System.Net.WebUtility.HtmlEncode(text);
    }
}
