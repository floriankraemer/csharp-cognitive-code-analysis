/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

namespace CognitiveCodeAnalysis.Tests.Application;

public class BaselineComparisonServiceTests
{
    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void CompareIfRequested_WithoutBaselineFile_ReturnsNull(string? baselineFile)
    {
        var service = new BaselineComparisonService();
        var metrics = new CognitiveMetricsCollection();

        var comparison = service.CompareIfRequested(baselineFile, metrics);

        Assert.That(comparison, Is.Null);
    }

    [Test]
    public void CompareIfRequested_WithBaselineFile_ReturnsComparison()
    {
        var baselineMetrics = SampleMetric(totalScore: 1.0);
        var snapshot = BaselineSnapshotFactory.FromMetricsCollection(new CognitiveMetricsCollection { baselineMetrics });
        var path = Path.Combine(Path.GetTempPath(), "baseline-svc-" + Guid.NewGuid() + ".json");

        try
        {
            File.WriteAllText(path, BaselineLoader.Serialize(snapshot));

            var currentMetrics = SampleMetric(totalScore: 2.0);
            var service = new BaselineComparisonService();

            var comparison = service.CompareIfRequested(path, new CognitiveMetricsCollection { currentMetrics });

            Assert.That(comparison, Is.Not.Null);
            Assert.That(comparison!.TryGetMethodComparison(currentMetrics, out MethodMetricsComparison? methodComparison), Is.True);
            Assert.That(methodComparison!.TotalScore.Delta, Is.EqualTo(1.0).Within(0.0001));
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
