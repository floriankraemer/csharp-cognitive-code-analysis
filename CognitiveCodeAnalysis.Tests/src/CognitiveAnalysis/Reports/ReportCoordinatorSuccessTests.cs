/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class ReportCoordinatorSuccessTests
{
    [TestCase("html")]
    [TestCase("HTML")]
    [TestCase("hTmL")]
    public void GenerateReport_ReportTypeIsMatchedCaseInsensitively_AndAnnouncedWithTheCanonicalName(string requested)
    {
        var coordinator = new ReportCoordinator(new IReport[] { new CsvReport(), new HtmlReport() });
        string? seenType = null;
        coordinator.ReportGenerated += (_, e) => seenType = e.ReportType;
        var path = Path.Combine(Path.GetTempPath(), "coord-" + Guid.NewGuid() + ".html");
        try
        {
            coordinator.GenerateReport(requested, path, new CognitiveConfiguration(), new CognitiveMetricsCollection());

            Assert.That(seenType, Is.EqualTo("Html"));
            Assert.That(File.ReadAllText(path), Does.Contain("<html"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void GenerateReport_UnknownReportType_StillThrowsAndListsTheSupportedOnes()
    {
        var coordinator = new ReportCoordinator(new IReport[] { new CsvReport(), new HtmlReport() });

        var ex = Assert.Throws<InvalidOperationException>(() => coordinator.GenerateReport(
            "htm", "ignored", new CognitiveConfiguration(), new CognitiveMetricsCollection()));

        Assert.That(ex!.Message, Does.Contain("Unknown report type 'htm'").And.Contain("Csv, Html"));
    }

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
