/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Baseline;

public class BaselineComparerTests
{
    [Test]
    public void Compare_ComputesDeltaForMatchedMethod()
    {
        var baselineMetrics = SampleMetric(totalScore: 2.0, ifCount: 1);
        var currentMetrics = SampleMetric(totalScore: 2.5, ifCount: 3);

        var baseline = BaselineSnapshotFactory.FromMetricsCollection(new CognitiveMetricsCollection { baselineMetrics });
        var current = new CognitiveMetricsCollection { currentMetrics };

        var comparison = BaselineComparer.Compare(current, baseline);

        Assert.That(comparison.TryGetMethodComparison(currentMetrics, out MethodMetricsComparison? methodComparison), Is.True);
        Assert.That(methodComparison!.TotalScore.Delta, Is.EqualTo(0.5).Within(0.0001));
        Assert.That(methodComparison.IfCount.Delta, Is.EqualTo(2));
    }

    [Test]
    public void Compare_NewMethod_HasNoBaselineDelta()
    {
        var baseline = BaselineSnapshotFactory.FromMetricsCollection(new CognitiveMetricsCollection());
        var currentMetrics = SampleMetric(totalScore: 1.0, ifCount: 0);
        var current = new CognitiveMetricsCollection { currentMetrics };

        var comparison = BaselineComparer.Compare(current, baseline);

        Assert.That(comparison.TryGetMethodComparison(currentMetrics, out MethodMetricsComparison? methodComparison), Is.True);
        Assert.That(methodComparison!.HasBaseline, Is.False);
        Assert.That(methodComparison.TotalScore.Delta, Is.Null);
    }

    [Test]
    public void Compare_MatchesByNormalizedPathAndSignature()
    {
        var baselineMetrics = SampleMetric(totalScore: 1.0, ifCount: 0);
        baselineMetrics.FilePath = @"src\Demo.cs";

        var currentMetrics = SampleMetric(totalScore: 2.0, ifCount: 1);
        currentMetrics.FilePath = "src/Demo.cs";
        currentMetrics.methodLineNumber = 99;

        var baseline = BaselineSnapshotFactory.FromMetricsCollection(new CognitiveMetricsCollection { baselineMetrics });
        var current = new CognitiveMetricsCollection { currentMetrics };

        var comparison = BaselineComparer.Compare(current, baseline);

        Assert.That(comparison.TryGetMethodComparison(currentMetrics, out MethodMetricsComparison? methodComparison), Is.True);
        Assert.That(methodComparison!.HasBaseline, Is.True);
        Assert.That(methodComparison.TotalScore.Delta, Is.EqualTo(1.0).Within(0.0001));
    }

    [Test]
    public void Compare_NullableCoverageDelta_OnlyWhenBothSidesHaveValues()
    {
        var baselineMetrics = SampleMetric(totalScore: 1.0, ifCount: 0);
        baselineMetrics.lineCoveragePercentage = 50.0;

        var currentWithCoverage = SampleMetric(totalScore: 1.0, ifCount: 0);
        currentWithCoverage.lineCoveragePercentage = 80.0;

        var baseline = BaselineSnapshotFactory.FromMetricsCollection(new CognitiveMetricsCollection { baselineMetrics });
        var current = new CognitiveMetricsCollection { currentWithCoverage };

        var comparison = BaselineComparer.Compare(current, baseline);

        Assert.That(comparison.TryGetMethodComparison(currentWithCoverage, out MethodMetricsComparison? methodComparison), Is.True);
        Assert.That(methodComparison!.LineCoveragePercentage.Delta, Is.EqualTo(30.0).Within(0.0001));
    }

    [Test]
    public void Compare_ClassCouplingDelta()
    {
        var metrics = SampleMetric(totalScore: 1.0, ifCount: 0);
        var current = new CognitiveMetricsCollection { metrics };
        current.SetClassCouplingMetrics(
        [
            new ClassCouplingMetrics { ClassName = "C", IncomingCoupling = 3, OutgoingCoupling = 2, Stability = 0.4 },
        ]);

        var baseline = BaselineSnapshotFactory.FromMetricsCollection(current);
        current.SetClassCouplingMetrics(
        [
            new ClassCouplingMetrics { ClassName = "C", IncomingCoupling = 5, OutgoingCoupling = 1, Stability = 0.2 },
        ]);

        var comparison = BaselineComparer.Compare(current, baseline);

        Assert.That(comparison.TryGetClassCouplingComparison("C", out ClassCouplingComparison? couplingComparison), Is.True);
        Assert.That(couplingComparison!.IncomingCoupling.Delta, Is.EqualTo(2));
        Assert.That(couplingComparison.OutgoingCoupling.Delta, Is.EqualTo(-1));
        Assert.That(couplingComparison.Stability.Delta, Is.EqualTo(-0.2).Within(0.0001));
    }

    private static CognitiveMetrics SampleMetric(double totalScore, int ifCount)
    {
        var m = new CognitiveMetrics(
            methodName: "Foo",
            className: "C",
            filePath: "src/Demo.cs",
            methodSignature: "void Foo()",
            methodLineNumber: 10,
            ifCount: ifCount
        );
        m.totalScore = totalScore;
        return m;
    }
}
