using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public class ReportFactory
{
    public event EventHandler<ReportGeneratedEventArgs>? ReportGenerated;

    public void GenerateReport(
        string reportType,
        string outputFile,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection metricsCollection
    )
    {
        ReportInterface reporter = reportType switch
        {
            "ConsoleText" => new ConsoleTextReport(configuration),
            "Html" => new HtmlReport(outputFile, configuration.GroupByClass),
            _ => throw new ArgumentException($"Invalid report type: {reportType}")
        };

        reporter.RenderMetrics(metricsCollection);

        string fullPath = Path.GetFullPath(outputFile);
        OnReportGenerated(reportType, fullPath);
    }

    protected virtual void OnReportGenerated(string reportType, string fullPath)
    {
        ReportGenerated?.Invoke(this, new ReportGeneratedEventArgs(reportType, fullPath));
    }
}

public class ReportGeneratedEventArgs : EventArgs
{
    public string ReportType { get; }
    public string FullPath { get; }

    public ReportGeneratedEventArgs(string reportType, string fullPath)
    {
        ReportType = reportType;
        FullPath = fullPath;
    }
}
