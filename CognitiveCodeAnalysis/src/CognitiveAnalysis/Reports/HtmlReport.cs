/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Text;

using CognitiveCodeAnalysis.CognitiveAnalysis;

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
        html.AppendLine("        .report-class-section:first-of-type .class-header { margin-top: 1rem; }");
        html.AppendLine("        .report-filter-bar { max-width: 32rem; }");
        html.AppendLine("    </style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("    <div class=\"container-fluid mt-4\">");
        html.AppendLine("        <h1 class=\"mb-4\">Cognitive Code Analysis Report</h1>");
        AppendFilterControls(html);

        CognitiveMetricsCollection filtered = ReportMetricsFilter.FilterForReport(metricsCollection, configuration);
        HandleGrouping(filtered, html, configuration, metricsCollection);

        html.AppendLine("    </div>");
        html.AppendLine("    <script src=\"https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/js/bootstrap.bundle.min.js\"></script>");
        AppendFilterScript(html);
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        WriteReportToFile(outputFile, html);
    }

    private void HandleGrouping(
        CognitiveMetricsCollection metricsCollection,
        StringBuilder html,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection fullMetricsCollection
    ) {
        if (configuration.GroupByClass)
        {
            html.Append(RenderMetricsGrouped(metricsCollection, configuration, fullMetricsCollection));
            return;
        }

        html.Append(RenderMetricsUngrouped(metricsCollection, configuration));
    }

    private static void AppendFilterControls(StringBuilder html)
    {
        html.AppendLine("        <div class=\"mb-4 report-filter-bar\">");
        html.AppendLine("            <label for=\"report-filter\" class=\"form-label\">Filter by class or file path</label>");
        html.AppendLine(
            "            <input type=\"search\" id=\"report-filter\" class=\"form-control\" placeholder=\"e.g. controller\" autocomplete=\"off\">");
        html.AppendLine(
            "            <div class=\"form-text\">Case-insensitive match on class name or full file path. Empty shows all sections.</div>");
        html.AppendLine("        </div>");
    }

    private static void AppendFilterScript(StringBuilder html)
    {
        html.AppendLine("    <script>");
        html.AppendLine("(function(){");
        html.AppendLine("  var input=document.getElementById('report-filter');");
        html.AppendLine("  if(!input)return;");
        html.AppendLine("  var sections=document.querySelectorAll('.report-class-section');");
        html.AppendLine("  function apply(){");
        html.AppendLine("    var q=input.value.trim().toLowerCase();");
        html.AppendLine("    sections.forEach(function(sec){");
        html.AppendLine("      if(!q){ sec.classList.remove('d-none'); return; }");
        html.AppendLine("      var cn=(sec.getAttribute('data-class-name')||'').toLowerCase();");
        html.AppendLine("      var fp=(sec.getAttribute('data-file-path')||'').toLowerCase();");
        html.AppendLine("      var show=cn.indexOf(q)!==-1||fp.indexOf(q)!==-1;");
        html.AppendLine("      sec.classList.toggle('d-none',!show);");
        html.AppendLine("    });");
        html.AppendLine("  }");
        html.AppendLine("  input.addEventListener('input',apply);");
        html.AppendLine("})();");
        html.AppendLine("    </script>");
    }

    private void WriteReportToFile(string outputFilePath, StringBuilder html)
    {
        string? directory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputFilePath, html.ToString());
    }

    private string RenderMetricsGrouped(
        CognitiveMetricsCollection metricsCollection,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection fullMetricsCollection
    )
    {
        var html = new StringBuilder();
        bool hasCoverageData = metricsCollection.HasCoverageData();
        var groupedByClass = metricsCollection
            .GroupBy(m => new { m.ClassName, m.FilePath })
            .OrderByDescending(g => g.Max(m => m.totalScore))
            .ThenBy(g => g.Key.ClassName);

        foreach (var classGroup in groupedByClass)
        {
            List<CognitiveMetrics> classMetrics = classGroup.OrderByDescending(m => m.totalScore).ToList();
            CognitiveMetrics firstMetric = classMetrics[0];

            html.AppendLine(
                $"        <div class=\"report-class-section\" data-class-name=\"{HtmlEncode(firstMetric.ClassName)}\" data-file-path=\"{HtmlEncode(firstMetric.FilePath)}\">");

            html.AppendLine("        <div class=\"class-header\">");
            html.AppendLine($"            <h3 class=\"text-primary\">Class: {HtmlEncode(firstMetric.ClassName)}</h3>");
            html.AppendLine($"            <p class=\"text-muted\">File: {HtmlEncode(firstMetric.FilePath)}</p>");
            AppendCouplingLine(html, configuration, fullMetricsCollection, firstMetric.ClassName);
            html.AppendLine("        </div>");

            AppendMetricsTable(html, classMetrics, configuration, hasCoverageData);

            html.AppendLine("        </div>");
        }

        return html.ToString();
    }

    private string RenderMetricsUngrouped(CognitiveMetricsCollection metricsCollection, CognitiveConfiguration configuration)
    {
        var html = new StringBuilder();
        bool hasCoverageData = metricsCollection.HasCoverageData();

        foreach (CognitiveMetrics metrics in metricsCollection.OrderByDescending(m => m.totalScore))
        {
            html.AppendLine(
                $"        <div class=\"report-class-section\" data-class-name=\"{HtmlEncode(metrics.ClassName)}\" data-file-path=\"{HtmlEncode(metrics.FilePath)}\">");

            html.AppendLine("        <div class=\"class-header\">");
            html.AppendLine($"            <h3 class=\"text-primary\">Class: {HtmlEncode(metrics.ClassName)}</h3>");
            html.AppendLine($"            <p class=\"text-success\">Method: {HtmlEncode(metrics.methodSignature)}</p>");
            html.AppendLine($"            <p class=\"text-muted\">File: {HtmlEncode(metrics.FilePath)}</p>");
            html.AppendLine("        </div>");

            AppendMetricsTable(html, [metrics], configuration, hasCoverageData);

            html.AppendLine("        </div>");
        }

        return html.ToString();
    }

    private static void AppendMetricsTable(
        StringBuilder html,
        IReadOnlyList<CognitiveMetrics> metricsList,
        CognitiveConfiguration configuration,
        bool hasCoverageData
    )
    {
        IReadOnlyList<string> headers = CognitiveReportTableFormat.BuildColumnHeaders(configuration, hasCoverageData);

        html.AppendLine("        <div class=\"table-responsive\">");
        html.AppendLine("            <table class=\"table table-striped table-bordered\">");
        html.AppendLine("                <thead class=\"table-dark\">");
        html.AppendLine("                    <tr>");
        foreach (string header in headers)
        {
            html.AppendLine($"                        <th>{HtmlEncode(header)}</th>");
        }
        html.AppendLine("                    </tr>");
        html.AppendLine("                </thead>");
        html.AppendLine("                <tbody>");

        foreach (CognitiveMetrics metrics in metricsList)
        {
            AppendMetricsRow(html, metrics, configuration, hasCoverageData);
        }

        html.AppendLine("                </tbody>");
        html.AppendLine("            </table>");
        html.AppendLine("        </div>");
    }

    private static void AppendMetricsRow(
        StringBuilder html,
        CognitiveMetrics metrics,
        CognitiveConfiguration configuration,
        bool hasCoverageData
    )
    {
        IReadOnlyList<string> cells = CognitiveReportTableFormat.BuildRowValues(metrics, configuration, hasCoverageData);
        int churnColumnIndex = FindChurnColumnIndex(configuration, hasCoverageData);

        html.AppendLine("                    <tr>");
        for (int i = 0; i < cells.Count; i++)
        {
            string cell = cells[i];
            if (i == 0)
            {
                html.AppendLine($"                        <td>{HtmlEncode(cell)}</td>");
            }
            else if (i == 1)
            {
                html.AppendLine(
                    $"                        <td><span class=\"{GetScoreClass(metrics.totalScore)}\">{HtmlEncode(cell)}</span></td>");
            }
            else if (hasCoverageData && i == churnColumnIndex)
            {
                double churnScoreForColor = metrics.churnScore ?? 0;
                html.AppendLine(
                    $"                        <td><span class=\"{GetChurnScoreClass(churnScoreForColor)}\">{HtmlEncode(cell)}</span></td>");
            }
            else
            {
                html.AppendLine($"                        <td>{HtmlEncode(cell)}</td>");
            }
        }
        html.AppendLine("                    </tr>");
    }

    private static int FindChurnColumnIndex(CognitiveConfiguration configuration, bool hasCoverageData)
    {
        if (!hasCoverageData)
        {
            return -1;
        }

        int index = 14;
        if (configuration.ShowHalsteadComplexity)
        {
            index += 3;
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            index += 1;
        }

        return index;
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

    private static string GetChurnScoreClass(double churnScore)
    {
        return churnScore switch
        {
            < 0.3 => "score-green",
            <= 0.7 => "score-yellow",
            _ => "score-red",
        };
    }

    private static void AppendCouplingLine(
        StringBuilder html,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection metricsCollection,
        string className
    ) {
        if (!configuration.GroupByClass || !configuration.ShowCouplingMetrics)
        {
            return;
        }

        string couplingText = FormatCouplingMetrics(metricsCollection, className);
        html.AppendLine($"            <p class=\"text-muted\">Coupling: {HtmlEncode(couplingText)}</p>");
    }

    private static string FormatCouplingMetrics(CognitiveMetricsCollection metricsCollection, string className)
    {
        if (!metricsCollection.TryGetClassCoupling(className, out var coupling) || coupling == null)
        {
            return "n/a";
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "In={0}, Out={1}, Stability={2:F3}",
            coupling.IncomingCoupling,
            coupling.OutgoingCoupling,
            coupling.Stability
        );
    }

    private static string HtmlEncode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return System.Net.WebUtility.HtmlEncode(text);
    }
}
