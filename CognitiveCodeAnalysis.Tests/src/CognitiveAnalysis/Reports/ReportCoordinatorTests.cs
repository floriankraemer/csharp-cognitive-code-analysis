using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class ReportCoordinatorTests
{
    [Test]
    public void GenerateReport_UnknownType_ThrowsWithSupportedList()
    {
        var coordinator = new ReportCoordinator(new IReport[] { new HtmlReport() });
        var config = new CognitiveConfiguration();
        var coll = new CognitiveMetricsCollection();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            coordinator.GenerateReport("NotARealReport", "out.txt", config, coll))!;

        Assert.That(ex.Message, Does.Contain("NotARealReport"));
        Assert.That(ex.Message, Does.Contain("Html"));
    }
}
