/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class CognitiveMetricsCollectionTests
{
    private static CognitiveMetrics M(string name, double score, string file = "F.cs", string cls = "C") =>
        new(name, cls, file, name + "()", 1) { totalScore = score };

    [Test]
    public void OnlyMetricsExceedingScoreThreshold_Filters()
    {
        var c = new CognitiveMetricsCollection { M("a", 1.0), M("b", 5.0) };
        var f = c.OnlyMetricsExceedingScoreThreshold(2.0);
        Assert.That(f.Count, Is.EqualTo(1));
        Assert.That(f[0].MethodName, Is.EqualTo("b"));
    }

    [Test]
    public void HasCoverageData_ReflectsMetrics()
    {
        var with = new CognitiveMetricsCollection { new("m", "C", "f.cs", "m()", 1, lineCoveragePercentage: 50) };
        var without = new CognitiveMetricsCollection { M("x", 1.0) };
        Assert.That(with.HasCoverageData(), Is.True);
        Assert.That(without.HasCoverageData(), Is.False);
    }

    [Test]
    public void GetTotalClasses_CountsDistinctClassAndFile()
    {
        var c = new CognitiveMetricsCollection { M("a", 1, "A.cs", "T1"), M("b", 1, "A.cs", "T1"), M("c", 1, "B.cs", "T2") };
        Assert.That(c.GetTotalClasses(), Is.EqualTo(2));
    }

    [Test]
    public void GetTotalMethods_ReturnsCount()
    {
        var c = new CognitiveMetricsCollection { M("a", 1), M("b", 1) };
        Assert.That(c.GetTotalMethods(), Is.EqualTo(2));
    }

    [Test]
    public void GetMethodsExceedingThreshold_Counts()
    {
        var c = new CognitiveMetricsCollection { M("a", 0.5), M("b", 2.0) };
        Assert.That(c.GetMethodsExceedingThreshold(1.0), Is.EqualTo(1));
    }

    [Test]
    public void GetClassesWithExceedingMethods_Counts()
    {
        var c = new CognitiveMetricsCollection
        {
            M("a", 0.5, "F.cs", "C1"),
            M("b", 2.0, "F.cs", "C1"),
            M("c", 2.0, "G.cs", "C2"),
        };
        Assert.That(c.GetClassesWithExceedingMethods(1.0), Is.EqualTo(2));
    }

    [Test]
    public void GetMethodsPercentage_EmptyReturnsZero()
    {
        var c = new CognitiveMetricsCollection();
        Assert.That(c.GetMethodsPercentage(1.0), Is.EqualTo(0.0));
    }

    [Test]
    public void GetMethodsPercentage_ComputesRatio()
    {
        var c = new CognitiveMetricsCollection { M("a", 2), M("b", 0.5), M("c", 3) };
        Assert.That(c.GetMethodsPercentage(1.0), Is.EqualTo(100.0 * 2.0 / 3.0).Within(0.0001));
    }

    [Test]
    public void GetClassesPercentage_EmptyReturnsZero()
    {
        var c = new CognitiveMetricsCollection();
        Assert.That(c.GetClassesPercentage(1.0), Is.EqualTo(0.0));
    }
}
