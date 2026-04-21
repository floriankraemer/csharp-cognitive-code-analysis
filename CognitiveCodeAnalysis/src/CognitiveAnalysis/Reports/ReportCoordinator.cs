using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public class ReportCoordinator(IEnumerable<IReport> reports)
{
    private readonly IReadOnlyCollection<IReport> _reportGenerators = reports.ToList().AsReadOnly();

    public event EventHandler<ReportGeneratedEventArgs>? ReportGenerated;

    public void GenerateReport(
        string reportType,
        string outputFile,
        CognitiveConfiguration configuration,
        CognitiveMetricsCollection metricsCollection
    )
    {
        foreach (IReport reportGenerator in _reportGenerators)
        {
            if (!reportGenerator.Name.Equals(reportType)) continue;

            reportGenerator.RenderMetrics(
                outputFile: outputFile,
                metricsCollection: metricsCollection,
                configuration: configuration
            );

            OnReportGenerated(
                reportType: reportType,
                fullPath: Path.GetFullPath(outputFile)
            );

            return;
        }

        var supported = string.Join(", ", _reportGenerators.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal));
        throw new InvalidOperationException(
            $"Unknown report type '{reportType}'. Supported report types: {supported}.");
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
