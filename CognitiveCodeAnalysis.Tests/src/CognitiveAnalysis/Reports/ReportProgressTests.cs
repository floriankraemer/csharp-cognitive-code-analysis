/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class ReportProgressTests
{
    [Test]
    public void CsvReport_RenderMetrics_ReportsPerMetricProgress()
    {
        var coll = new CognitiveMetricsCollection
        {
            SampleMetric("A", 1),
            SampleMetric("B", 2),
            SampleMetric("C", 3),
        };
        var config = new CognitiveConfiguration { ShowOnlyMethodsExceedingThreshold = false };
        var collector = new AnalysisProgressCollector();
        var path = Path.Combine(Path.GetTempPath(), "csv-progress-" + Guid.NewGuid() + ".csv");

        try
        {
            new CsvReport().RenderMetrics(path, coll, config, progress: collector);

            var reports = collector.Reports;
            var writingReports = reports.Where(r => r.Phase == AnalysisProgressPhase.WritingReport).ToList();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(writingReports, Has.Count.EqualTo(4));
                Assert.That(writingReports[0].ProcessedFiles, Is.EqualTo(0));
                Assert.That(writingReports[0].TotalFiles, Is.EqualTo(3));
                Assert.That(writingReports.Skip(1).Select(r => r.ProcessedFiles), Is.EquivalentTo(new[] { 1, 2, 3 }));
                Assert.That(reports[^1].Phase, Is.EqualTo(AnalysisProgressPhase.ReportCompleted));
                Assert.That(reports[^1].ReportName, Is.EqualTo("Csv"));
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void GenerateReport_PassesProgressToReport()
    {
        var collector = new AnalysisProgressCollector();
        var stub = new ProgressCapturingReport();
        var coordinator = new ReportCoordinator([stub]);
        var path = Path.Combine(Path.GetTempPath(), "coord-progress-" + Guid.NewGuid() + ".txt");

        try
        {
            coordinator.GenerateReport(
                "Stub",
                path,
                new CognitiveConfiguration(),
                new CognitiveMetricsCollection(),
                progress: collector
            );

            Assert.That(stub.ReceivedProgress, Is.SameAs(collector));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static CognitiveMetrics SampleMetric(string methodName, int line)
    {
        var m = new CognitiveMetrics(
            methodName: methodName,
            className: "C",
            filePath: "/tmp/Sample.cs",
            methodSignature: $"void {methodName}()",
            methodLineNumber: line
        );
        m.totalScore = line;
        return m;
    }

    private sealed class ProgressCapturingReport : IReport
    {
        public string Name => "Stub";

        public IProgress<AnalysisProgress>? ReceivedProgress { get; private set; }

        public void RenderMetrics(
            string outputFile,
            CognitiveMetricsCollection metricsCollection,
            CognitiveConfiguration configuration,
            CognitiveBaselineComparison? baselineComparison = null,
            IProgress<AnalysisProgress>? progress = null
        )
        {
            ReceivedProgress = progress;
            File.WriteAllText(outputFile, "stub");
        }
    }
}
