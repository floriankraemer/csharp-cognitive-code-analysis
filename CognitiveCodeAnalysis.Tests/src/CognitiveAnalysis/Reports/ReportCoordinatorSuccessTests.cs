/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class ReportCoordinatorSuccessTests
{
    [Test]
    public void GenerateReport_Html_RaisesReportGenerated()
    {
        var coordinator = new ReportCoordinator(new IReport[] { new HtmlReport() });
        string? seenType = null;
        string? seenPath = null;
        coordinator.ReportGenerated += (_, e) =>
        {
            seenType = e.ReportType;
            seenPath = e.FullPath;
        };

        var path = Path.Combine(Path.GetTempPath(), "coord-" + Guid.NewGuid() + ".html");
        try
        {
            coordinator.GenerateReport("Html", path, new CognitiveConfiguration(), new CognitiveMetricsCollection());
            Assert.That(seenType, Is.EqualTo("Html"));
            Assert.That(seenPath, Is.EqualTo(Path.GetFullPath(path)));
            Assert.That(File.Exists(path), Is.True);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
