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
        // Ensure directory exists
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
            AppendHalsteadCyclomaticHeaders(html, configuration);
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
                html.AppendLine($"                        <td><span class=\"{GetScoreClass(metrics.totalScore)}\">{FormatInvariant(metrics.totalScore, "F3")}</span></td>");
                html.AppendLine($"                        <td>{metrics.linesOfCode}</td>");
                html.AppendLine($"                        <td>{metrics.ifCount} ({FormatInvariant(metrics.ifScore, "F3")})</td>");
                html.AppendLine($"                        <td>{metrics.argumentCount} ({FormatInvariant(metrics.argumentScore, "F3")})</td>");
                html.AppendLine($"                        <td>{metrics.nestingLevels} ({FormatInvariant(metrics.nestingScore, "F3")})</td>");
                html.AppendLine($"                        <td>{metrics.returnCount} ({FormatInvariant(metrics.returnScore, "F3")})</td>");
                AppendHalsteadCyclomaticCells(html, metrics, configuration);
                if (hasCoverageData)
                {
                    string churnValue = metrics.churnScore.HasValue 
                        ? FormatInvariant(metrics.churnScore.Value, "F3")
                        : "n/a";
                    double churnScoreForColor = metrics.churnScore ?? 0;
                    html.AppendLine($"                        <td><span class=\"{GetChurnScoreClass(churnScoreForColor)}\">{churnValue}</span></td>");
                }
                html.AppendLine("                    </tr>");
            }

            html.AppendLine("                </tbody>");
            html.AppendLine("            </table>");
            html.AppendLine("        </div>");
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
            AppendHalsteadCyclomaticHeaders(html, configuration);
            if (hasCoverageData)
            {
                html.AppendLine("                        <th>Churn</th>");
            }
            html.AppendLine("                    </tr>");
            html.AppendLine("                </thead>");
            html.AppendLine("                <tbody>");
            html.AppendLine("                    <tr>");
            html.AppendLine($"                        <td>L{metrics.methodLineNumber} {HtmlEncode(metrics.MethodName)}</td>");
            html.AppendLine($"                        <td><span class=\"{GetScoreClass(metrics.totalScore)}\">{FormatInvariant(metrics.totalScore, "F3")}</span></td>");
            html.AppendLine($"                        <td>{metrics.linesOfCode}</td>");
            html.AppendLine($"                        <td>{metrics.ifCount} ({FormatInvariant(metrics.ifScore, "F3")})</td>");
            html.AppendLine($"                        <td>{metrics.argumentCount} ({FormatInvariant(metrics.argumentScore, "F3")})</td>");
            html.AppendLine($"                        <td>{metrics.nestingLevels} ({FormatInvariant(metrics.nestingScore, "F3")})</td>");
            html.AppendLine($"                        <td>{metrics.returnCount} ({FormatInvariant(metrics.returnScore, "F3")})</td>");
            AppendHalsteadCyclomaticCells(html, metrics, configuration);
            if (hasCoverageData)
            {
                string churnValue = metrics.churnScore.HasValue 
                    ? FormatInvariant(metrics.churnScore.Value, "F3")
                    : "n/a";
                double churnScoreForColor = metrics.churnScore ?? 0;
                html.AppendLine($"                        <td><span class=\"{GetChurnScoreClass(churnScoreForColor)}\">{churnValue}</span></td>");
            }
            html.AppendLine("                    </tr>");
            html.AppendLine("                </tbody>");
            html.AppendLine("            </table>");
            html.AppendLine("        </div>");
            html.AppendLine("        </div>");
        }

        return html.ToString();
    }

    private static void AppendHalsteadCyclomaticHeaders(StringBuilder html, CognitiveConfiguration configuration)
    {
        if (configuration.ShowHalsteadComplexity)
        {
            html.AppendLine("                        <th>Halstead Volume</th>");
            html.AppendLine("                        <th>Halstead Difficulty</th>");
            html.AppendLine("                        <th>Halstead Effort</th>");
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            html.AppendLine("                        <th>Cyclomatic Complexity</th>");
        }
    }

    private static void AppendHalsteadCyclomaticCells(StringBuilder html, CognitiveMetrics metrics, CognitiveConfiguration configuration)
    {
        if (configuration.ShowHalsteadComplexity)
        {
            html.AppendLine($"                        <td>{FormatHalsteadDouble(metrics.Halstead?.Volume)}</td>");
            html.AppendLine($"                        <td>{FormatHalsteadDouble(metrics.Halstead?.Difficulty)}</td>");
            html.AppendLine($"                        <td>{FormatHalsteadDouble(metrics.Halstead?.Effort)}</td>");
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            html.AppendLine($"                        <td>{FormatInvariant(metrics.cyclomaticComplexity, "F1")}</td>");
        }
    }

    private static string FormatInvariant(double value, string format)
        => value.ToString(format, CultureInfo.InvariantCulture);

    private static string FormatHalsteadDouble(double? value)
        => value.HasValue ? value.Value.ToString("F2", CultureInfo.InvariantCulture) : "n/a";

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

        return string.Create(
            CultureInfo.InvariantCulture,
            $"In={coupling.IncomingCoupling}, Out={coupling.OutgoingCoupling}, Stability={coupling.Stability:F3}"
        );
    }

    private static string HtmlEncode(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        return System.Net.WebUtility.HtmlEncode(text);
    }
}
