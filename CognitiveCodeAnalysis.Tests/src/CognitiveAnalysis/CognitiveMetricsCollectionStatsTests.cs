/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class CognitiveMetricsCollectionStatsTests
{
    private static CognitiveMetrics M(string cls, string file, double score) =>
        new("m", cls, file, "void m()", 1) { totalScore = score };

    private static CognitiveMetricsCollection Sample() =>
    [
        M("A", "a.cs", 1.0),
        M("A", "a.cs", 3.0),
        M("B", "b.cs", 2.0),
        M("A", "other/a.cs", 0.5),
    ];

    [Test]
    public void Totals_GroupClassesByNameAndFile()
    {
        var c = Sample();

        Assert.That(c.GetTotalClasses(), Is.EqualTo(3));
        Assert.That(c.GetTotalMethods(), Is.EqualTo(4));
    }

    [Test]
    public void ThresholdCounts_AreStrictlyGreaterThan()
    {
        var c = Sample();

        Assert.That(c.GetMethodsExceedingThreshold(2.0), Is.EqualTo(1));
        Assert.That(c.GetMethodsExceedingThreshold(1.99), Is.EqualTo(2));
        Assert.That(c.GetClassesWithExceedingMethods(2.0), Is.EqualTo(1));
        Assert.That(c.GetClassesWithExceedingMethods(1.99), Is.EqualTo(2));
        Assert.That(c.OnlyMetricsExceedingScoreThreshold(2.0), Has.Count.EqualTo(1));
        Assert.That(c.OnlyMetricsExceedingScoreThreshold(0.5), Has.Count.EqualTo(3));
    }

    [Test]
    public void Percentages_AreRatiosTimesHundred()
    {
        var c = Sample();

        Assert.That(c.GetMethodsPercentage(1.99), Is.EqualTo(50.0));
        Assert.That(c.GetClassesPercentage(1.99), Is.EqualTo(200.0 / 3.0).Within(1e-9));
        Assert.That(c.GetMethodsPercentage(100), Is.EqualTo(0.0));
    }

    [Test]
    public void Percentages_OfEmptyCollection_AreZero()
    {
        var c = new CognitiveMetricsCollection();

        Assert.That(c.GetMethodsPercentage(0), Is.EqualTo(0.0));
        Assert.That(c.GetClassesPercentage(0), Is.EqualTo(0.0));
    }

    [Test]
    public void SetClassCouplingMetrics_ReplacesPreviousEntries()
    {
        var c = new CognitiveMetricsCollection();
        c.SetClassCouplingMetrics([new ClassCouplingMetrics { ClassName = "A" }]);
        c.SetClassCouplingMetrics([new ClassCouplingMetrics { ClassName = "B" }]);

        Assert.That(c.TryGetClassCoupling("A", out var a), Is.False);
        Assert.That(a, Is.Null);
        Assert.That(c.TryGetClassCoupling("B", out var b), Is.True);
        Assert.That(b!.ClassName, Is.EqualTo("B"));
    }

    [Test]
    public void HasCoverageData_TrueWhenAnyMethodHasLineOrBranchCoverage()
    {
        var c = Sample();
        Assert.That(c.HasCoverageData(), Is.False);

        c[2].branchCoveragePercentage = 10;
        Assert.That(c.HasCoverageData(), Is.True);

        c[2].branchCoveragePercentage = null;
        c[1].lineCoveragePercentage = 10;
        Assert.That(c.HasCoverageData(), Is.True);
    }
}
