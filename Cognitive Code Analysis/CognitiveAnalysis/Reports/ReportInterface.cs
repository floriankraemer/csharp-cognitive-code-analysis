using System.Collections.ObjectModel;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
public interface ReportInterface
{
    void RenderMetrics(Collection<CognitiveMetrics> metricsCollection);
}
