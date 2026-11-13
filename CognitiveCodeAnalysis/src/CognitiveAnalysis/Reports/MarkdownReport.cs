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
            AppendGrouped(filtered, md, configuration, metricsCollection, baselineComparison, Name, progress, totalItems, ref processedItems);
        }
        else
        {
            AppendUngrouped(filtered, md, configuration, baselineComparison, Name, progress, totalItems, ref processedItems);
        }

        CognitiveReportFileWriter.Write(outputFile, md.ToString());
        ReportProgress.ReportComplete(progress, Name, totalItems);
    }

    private static void AppendGrouped(
        CognitiveMetricsCollection metricsCollection,
        StringBuilder md,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection fullMetricsCollection,
        CognitiveBaselineComparison? baselineComparison,
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
            List<CognitiveMetrics> classMetrics = ReportMetricsFilter.OrderRows(classGroup, configuration).ToList();
            CognitiveMetrics firstMetric = classMetrics[0];

            md.AppendLine($"### Class: {firstMetric.ClassName}");
            md.AppendLine();
            md.AppendLine($"**File:** `{firstMetric.FilePath}`");
            md.AppendLine();
            AppendCouplingLine(md, configuration, fullMetricsCollection, firstMetric.ClassName, baselineComparison);
            AppendMetricsTable(md, classMetrics, configuration, hasCoverageData, baselineComparison);

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
        CognitiveBaselineComparison? baselineComparison,
        string reportName,
        IProgress<AnalysisProgress>? progress,
        int totalItems,
        ref int processedItems
    )
    {
        bool hasCoverageData = metricsCollection.HasCoverageData();

        foreach (CognitiveMetrics metrics in ReportMetricsFilter.OrderRows(metricsCollection, configuration))
        {
            md.AppendLine($"### Class: {metrics.ClassName}");
            md.AppendLine();
            md.AppendLine($"**Method:** `{metrics.methodSignature}`");
            md.AppendLine();
            md.AppendLine($"**File:** `{metrics.FilePath}`");
            md.AppendLine();
            AppendMetricsTable(md, [metrics], configuration, hasCoverageData, baselineComparison);
            processedItems++;
            ReportProgress.ReportItem(progress, reportName, totalItems, processedItems);
        }
    }

    private static void AppendMetricsTable(
        StringBuilder md,
        IReadOnlyList<CognitiveMetrics> metricsList,
        CognitiveConfiguration configuration,
        bool hasCoverageData,
        CognitiveBaselineComparison? baselineComparison
    )
    {
        IReadOnlyList<string> headers = CognitiveReportTableFormat.BuildColumnHeaders(configuration, hasCoverageData);

        md.AppendLine("| " + string.Join(" | ", headers.Select(CognitiveReportTableFormat.EscapeMarkdownTableCell)) + " |");
        md.AppendLine("| " + string.Join(" | ", headers.Select((_, index) => index == 0 ? ":---" : "---:")) + " |");

        foreach (CognitiveMetrics metrics in metricsList)
        {
            MethodMetricsComparison? comparison = null;
            baselineComparison?.TryGetMethodComparison(metrics, out comparison);
            IReadOnlyList<string> cells = CognitiveReportTableFormat.BuildRowValues(metrics, configuration, hasCoverageData, comparison);
            md.AppendLine("| " + string.Join(" | ", cells.Select(CognitiveReportTableFormat.EscapeMarkdownTableCell)) + " |");
        }

        md.AppendLine();
    }

    private static void AppendCouplingLine(
        StringBuilder md,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection metricsCollection,
        string className,
        CognitiveBaselineComparison? baselineComparison
    )
    {
        if (!configuration.GroupByClass || !configuration.ShowCouplingMetrics)
        {
            return;
        }

        string couplingText = FormatCouplingMetrics(metricsCollection, className, baselineComparison);
        md.AppendLine($"**Coupling:** {couplingText}");
        md.AppendLine();
    }

    private static string FormatCouplingMetrics(
        CognitiveMetricsCollection metricsCollection,
        string className,
        CognitiveBaselineComparison? baselineComparison
    )
    {
        if (!metricsCollection.TryGetClassCoupling(className, out var coupling) || coupling == null)
        {
            return "n/a";
        }

        ClassCouplingComparison? couplingComparison = null;
        baselineComparison?.TryGetClassCouplingComparison(className, out couplingComparison);

        string incoming = coupling.IncomingCoupling.ToString(CultureInfo.InvariantCulture);
        string outgoing = coupling.OutgoingCoupling.ToString(CultureInfo.InvariantCulture);
        string stability = coupling.Stability.ToString("F3", CultureInfo.InvariantCulture);

        if (couplingComparison is { HasBaseline: true })
        {
            incoming = CognitiveReportDeltaFormatter.FormatMarkdownValue(incoming, couplingComparison.IncomingCoupling, "F0");
            outgoing = CognitiveReportDeltaFormatter.FormatMarkdownValue(outgoing, couplingComparison.OutgoingCoupling, "F0");
            stability = CognitiveReportDeltaFormatter.FormatMarkdownValue(stability, couplingComparison.Stability, "F3");
        }

        return $"In={incoming}, Out={outgoing}, Stability={stability}";
    }
}
