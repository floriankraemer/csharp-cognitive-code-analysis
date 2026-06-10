/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text;

using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public sealed class GithubActionsReport : IReport
{
    private const string CheckName = "cognitive/method-complexity";

    public string Name => "GithubActions";

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

        var sb = new StringBuilder();
        foreach (var m in filtered)
        {
            var kind = CognitiveCiSeverity.GithubCommandKind(m, configuration);
            var path = CognitiveCiEncoding.NormalizeFilePath(m.FilePath);
            var title = $"{CheckName} score {m.totalScore:F3}";
            var message = CognitiveCiEncoding.EncodeWorkflowCommandMessage(
                CognitiveCiSeverity.BuildMessage(m, configuration, baselineComparison));
            sb.Append("::").Append(kind)
                .Append(" file=").Append(path)
                .Append(",line=").Append(m.methodLineNumber)
                .Append(",title=").Append(CognitiveCiEncoding.EncodeWorkflowCommandMessage(title))
                .Append("::").Append(message).AppendLine();
            processedItems++;
            ReportProgress.ReportItem(progress, Name, totalItems, processedItems);
        }

        CognitiveReportFileWriter.Write(outputFile, sb.ToString());
        ReportProgress.ReportComplete(progress, Name, totalItems);
    }
}
