/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.Json;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class JsonReportTests
{
    [Test]
    public void RenderMetrics_WithoutBaseline_WritesBaselineSnapshotJson()
    {
        var metrics = SampleMetric(totalScore: 3.5);
        var collection = new CognitiveMetricsCollection { metrics };
        var path = Path.Combine(Path.GetTempPath(), "json-report-" + Guid.NewGuid() + ".json");

        try
        {
            new JsonReport().RenderMetrics(path, collection, new CognitiveConfiguration());

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;
            Assert.That(root.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(CognitiveBaselineSnapshot.CurrentSchemaVersion));
            Assert.That(root.GetProperty("methods").GetArrayLength(), Is.EqualTo(1));
            Assert.That(root.GetProperty("methods")[0].GetProperty("methodName").GetString(), Is.EqualTo("Alpha"));
            Assert.That(root.GetProperty("methods")[0].GetProperty("totalScore").GetDouble(), Is.EqualTo(3.5).Within(0.0001));
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
    public void RenderMetrics_WithBaseline_WritesMethodsAndCouplingDeltas()
    {
        var baselineMetrics = SampleMetric(totalScore: 1.0);

        var currentMetrics = SampleMetric(totalScore: 2.5);
        var current = new CognitiveMetricsCollection { currentMetrics };
        current.SetClassCouplingMetrics(
        [
            new ClassCouplingMetrics { ClassName = "Demo", IncomingCoupling = 4, OutgoingCoupling = 2, Stability = 0.33 },
        ]);

        var baselineCollection = new CognitiveMetricsCollection { baselineMetrics };
        baselineCollection.SetClassCouplingMetrics(
        [
            new ClassCouplingMetrics { ClassName = "Demo", IncomingCoupling = 2, OutgoingCoupling = 2, Stability = 0.5 },
        ]);
        var baselineSnapshot = BaselineSnapshotFactory.FromMetricsCollection(baselineCollection);
        var comparison = BaselineComparer.Compare(current, baselineSnapshot);

        var path = Path.Combine(Path.GetTempPath(), "json-report-delta-" + Guid.NewGuid() + ".json");
        try
        {
            new JsonReport().RenderMetrics(path, current, new CognitiveConfiguration(), comparison);

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var method = doc.RootElement.GetProperty("methods")[0];
            Assert.That(method.GetProperty("deltas").GetProperty("totalScore").GetDouble(), Is.EqualTo(1.5).Within(0.0001));

            var coupling = doc.RootElement.GetProperty("classCoupling")[0];
            Assert.That(coupling.GetProperty("deltas").GetProperty("incomingCoupling").GetInt32(), Is.EqualTo(2));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static CognitiveMetrics SampleMetric(double totalScore)
    {
        var metrics = new CognitiveMetrics(
            methodName: "Alpha",
            className: "Demo",
            filePath: "src/Demo.cs",
            methodSignature: "void Alpha()",
            methodLineNumber: 10
        );
        metrics.totalScore = totalScore;
        return metrics;
    }
}
