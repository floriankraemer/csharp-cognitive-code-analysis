/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Text;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public sealed class MarkdownReport : IReport
{
    public string Name => "Markdown";

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

        var md = new StringBuilder();
        md.AppendLine("# Cognitive Code Analysis Report");
        md.AppendLine();

        if (configuration.GroupByClass)
        {
            AppendGrouped(filtered, md, configuration, metricsCollection, Name, progress, totalItems, ref processedItems);
        }
        else
        {
            AppendUngrouped(filtered, md, configuration, Name, progress, totalItems, ref processedItems);
        }

        CognitiveReportFileWriter.Write(outputFile, md.ToString());
        ReportProgress.ReportComplete(progress, Name, totalItems);
    }

    private static void AppendGrouped(
        CognitiveMetricsCollection metricsCollection,
        StringBuilder md,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection fullMetricsCollection,
        string reportName,
        IProgress<AnalysisProgress>? progress,
        int totalItems,
        ref int processedItems
    )
    {
        bool hasCoverageData = metricsCollection.HasCoverageData();
        var groupedByClass = metricsCollection
            .GroupBy(m => new { m.ClassName, m.FilePath })
            .OrderByDescending(g => g.Max(m => m.totalScore))
            .ThenBy(g => g.Key.ClassName);

        foreach (var classGroup in groupedByClass)
        {
            List<CognitiveMetrics> classMetrics = classGroup.OrderByDescending(m => m.totalScore).ToList();
            CognitiveMetrics firstMetric = classMetrics[0];

            md.AppendLine($"### Class: {firstMetric.ClassName}");
            md.AppendLine();
            md.AppendLine($"**File:** `{firstMetric.FilePath}`");
            md.AppendLine();
            AppendCouplingLine(md, configuration, fullMetricsCollection, firstMetric.ClassName);
            AppendMetricsTable(md, classMetrics, configuration, hasCoverageData);

            foreach (var _ in classMetrics)
            {
                processedItems++;
                ReportProgress.ReportItem(progress, reportName, totalItems, processedItems);
            }
        }
    }

    private static void AppendUngrouped(
        CognitiveMetricsCollection metricsCollection,
        StringBuilder md,
        CognitiveConfiguration configuration,
        string reportName,
        IProgress<AnalysisProgress>? progress,
        int totalItems,
        ref int processedItems
    )
    {
        bool hasCoverageData = metricsCollection.HasCoverageData();

        foreach (CognitiveMetrics metrics in metricsCollection.OrderByDescending(m => m.totalScore))
        {
            md.AppendLine($"### Class: {metrics.ClassName}");
            md.AppendLine();
            md.AppendLine($"**Method:** `{metrics.methodSignature}`");
            md.AppendLine();
            md.AppendLine($"**File:** `{metrics.FilePath}`");
            md.AppendLine();
            AppendMetricsTable(md, [metrics], configuration, hasCoverageData);
            processedItems++;
            ReportProgress.ReportItem(progress, reportName, totalItems, processedItems);
        }
    }

    private static void AppendMetricsTable(
        StringBuilder md,
        IReadOnlyList<CognitiveMetrics> metricsList,
        CognitiveConfiguration configuration,
        bool hasCoverageData
    )
    {
        IReadOnlyList<string> headers = CognitiveReportTableFormat.BuildColumnHeaders(configuration, hasCoverageData);

        md.AppendLine("| " + string.Join(" | ", headers.Select(CognitiveReportTableFormat.EscapeMarkdownTableCell)) + " |");
        md.AppendLine("| " + string.Join(" | ", headers.Select(_ => "---:")) + " |");

        foreach (CognitiveMetrics metrics in metricsList)
        {
            IReadOnlyList<string> cells = CognitiveReportTableFormat.BuildRowValues(metrics, configuration, hasCoverageData);
            md.AppendLine("| " + string.Join(" | ", cells.Select(CognitiveReportTableFormat.EscapeMarkdownTableCell)) + " |");
        }

        md.AppendLine();
    }

    private static void AppendCouplingLine(
        StringBuilder md,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection metricsCollection,
        string className
    )
    {
        if (!configuration.GroupByClass || !configuration.ShowCouplingMetrics)
        {
            return;
        }

        string couplingText = FormatCouplingMetrics(metricsCollection, className);
        md.AppendLine($"**Coupling:** {couplingText}");
        md.AppendLine();
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
}
