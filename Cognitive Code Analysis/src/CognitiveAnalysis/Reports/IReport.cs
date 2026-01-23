namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public interface IReport
{
    void RenderMetrics(CognitiveMetricsCollection metricsCollection);
}
