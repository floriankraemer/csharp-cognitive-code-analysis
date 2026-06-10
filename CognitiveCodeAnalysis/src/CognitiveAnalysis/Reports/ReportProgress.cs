/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public static class ReportProgress
{
    public static void ReportStart(IProgress<AnalysisProgress>? progress, string reportName, int totalItems)
    {
        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.WritingReport,
            TotalFiles: totalItems,
            ProcessedFiles: 0,
            ReportName: reportName
        ));
    }

    public static void ReportItem(IProgress<AnalysisProgress>? progress, string reportName, int totalItems, int processedItems)
    {
        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.WritingReport,
            TotalFiles: totalItems,
            ProcessedFiles: processedItems,
            ReportName: reportName
        ));
    }

    public static void ReportComplete(IProgress<AnalysisProgress>? progress, string reportName, int totalItems)
    {
        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.ReportCompleted,
            TotalFiles: totalItems,
            ProcessedFiles: totalItems,
            ReportName: reportName
        ));
    }
}
