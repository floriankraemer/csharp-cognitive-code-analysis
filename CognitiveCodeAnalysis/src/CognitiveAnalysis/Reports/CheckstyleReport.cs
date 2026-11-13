/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Xml.Linq;

using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

/// <summary>
/// Checkstyle XML for CI tools such as Jenkins and the Checkstyle plugins. One <c>file</c> element per file with
/// violations and one <c>error</c> per method above the threshold. Methods at or below the threshold are not listed.
/// </summary>
public sealed class CheckstyleReport : IReport
{
    private const string Version = "8.0";
    private const string Source = "CognitiveComplexity";

    public string Name => "Checkstyle";

    public void RenderMetrics(
        string outputFile,
        CognitiveMetricsCollection metricsCollection,
        CognitiveConfiguration configuration,
        CognitiveBaselineComparison? baselineComparison = null,
        IProgress<AnalysisProgress>? progress = null
    )
    {
        var filtered = ReportMetricsFilter.FilterForReport(metricsCollection, configuration);
        int totalItems = filtered.Count;
        int processedItems = 0;

        ReportProgress.ReportStart(progress, Name, totalItems);

        var root = new XElement("checkstyle", new XAttribute("version", Version));
        var fileElements = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var m in ReportMetricsFilter.SortIfRequested(filtered, configuration))
        {
            if (CognitiveCiSeverity.IsViolation(m, configuration))
            {
                string path = CognitiveCiLocation.DisplayPath(m.FilePath);
                if (!fileElements.TryGetValue(path, out XElement? file))
                {
                    file = new XElement("file", new XAttribute("name", path));
                    fileElements[path] = file;
                    root.Add(file);
                }

                file.Add(ErrorElement(m, configuration));
            }

            processedItems++;
            ReportProgress.ReportItem(progress, Name, totalItems, processedItems);
        }

        CognitiveReportFileWriter.Write(outputFile, CognitiveCiXml.Serialize(root));
        ReportProgress.ReportComplete(progress, Name, totalItems);
    }

    private static XElement ErrorElement(CognitiveMetrics m, CognitiveConfiguration configuration) =>
        new(
            "error",
            new XAttribute("line", CognitiveCiLocation.Line(m)),
            new XAttribute("column", 1),
            new XAttribute("severity", CognitiveCiSeverity.IsError(m, configuration) ? "error" : "warning"),
            new XAttribute("message", CognitiveCiSeverity.ComplexityMessage(m, configuration)),
            new XAttribute("source", Source));
}
