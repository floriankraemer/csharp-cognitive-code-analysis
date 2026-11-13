/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Churn;
using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.Churn;

public class GitChurnCalculatorTests
{
    private sealed class FakeCounter(Dictionary<string, int> changes) : IChangeCounter
    {
        public List<string> Calls { get; } = [];

        public int Count(string file)
        {
            Calls.Add(file);
            return changes[file];
        }
    }

    private static CognitiveMetrics Method(string className, string file, double score, string name = "M") =>
        new(name, className, file, $"void {name}()", 1) { totalScore = score };

    private static Coverage ClassCoverage(string className, int covered, int total) =>
        new() { FullyQualifiedClassName = className, LinesCovered = covered, LinesTotal = total };

    [Test]
    public void Calculate_SumsMethodScoresPerClassAndMultipliesWithChanges()
    {
        var counter = new FakeCounter(new() { ["/a.cs"] = 4 });

        List<ChurnMetrics> result = GitChurnCalculator.Calculate(
            [Method("N.A", "/a.cs", 1.5, "X"), Method("N.A", "/a.cs", 0.25, "Y")], counter);

        Assert.That(result, Has.Count.EqualTo(1));
        Assert.That(result[0], Is.EqualTo(new ChurnMetrics("N.A", "/a.cs", 1.75, 4, 7)));
        Assert.That(result[0].Coverage, Is.Null);
        Assert.That(result[0].RiskChurn, Is.Null);
        Assert.That(result[0].RiskLevel, Is.Null);
    }

    [Test]
    public void Calculate_AsksGitOncePerFileHoweverManyClassesAndMethodsItHolds()
    {
        var counter = new FakeCounter(new() { ["/a.cs"] = 2, ["/b.cs"] = 3 });

        GitChurnCalculator.Calculate(
            [
                Method("N.A", "/a.cs", 1), Method("N.A", "/a.cs", 1, "Y"), Method("N.B", "/a.cs", 1),
                Method("N.C", "/b.cs", 1),
            ],
            counter);

        Assert.That(counter.Calls, Is.EqualTo(new[] { "/a.cs", "/b.cs" }));
    }

    [Test]
    public void Calculate_SortsByChurnDescendingThenByClassName()
    {
        var counter = new FakeCounter(new() { ["/1"] = 1, ["/2"] = 2, ["/3"] = 1, ["/4"] = 1 });

        List<ChurnMetrics> result = GitChurnCalculator.Calculate(
            [Method("N.B", "/1", 2), Method("N.C", "/2", 5), Method("N.A", "/3", 2), Method("N.D", "/4", 9)],
            counter);

        Assert.That(result.Select(metric => metric.ClassName), Is.EqualTo(new[] { "N.C", "N.D", "N.A", "N.B" }));
    }

    [Test]
    public void Calculate_SortsClassNamesOrdinally()
    {
        var counter = new FakeCounter(new() { ["/f"] = 1 });

        List<ChurnMetrics> result = GitChurnCalculator.Calculate(
            [Method("b", "/f", 1), Method("B", "/f", 1), Method("a", "/f", 1)], counter);

        Assert.That(result.Select(metric => metric.ClassName), Is.EqualTo(new[] { "B", "a", "b" }));
    }

    [Test]
    public void Calculate_MergesAClassNameInSeveralFilesKeepingFirstFileAndHighestChangeCount()
    {
        var counter = new FakeCounter(new() { ["/one.cs"] = 2, ["/two.cs"] = 5 });

        List<ChurnMetrics> result = GitChurnCalculator.Calculate(
            [Method("N.A", "/one.cs", 1), Method("N.A", "/two.cs", 2)], counter);

        Assert.That(result.Single(), Is.EqualTo(new ChurnMetrics("N.A", "/one.cs", 3, 5, 15)));
    }

    [Test]
    public void Calculate_KeepsClassesThatNeverChangedWithZeroChurn()
    {
        var counter = new FakeCounter(new() { ["/a.cs"] = 0 });

        ChurnMetrics metric = GitChurnCalculator.Calculate([Method("N.A", "/a.cs", 3)], counter).Single();

        Assert.That(metric.Churn, Is.Zero);
        Assert.That(metric.TimesChanged, Is.Zero);
    }

    [Test]
    public void Calculate_RoundsScoreAndChurnToThreeDecimals()
    {
        var counter = new FakeCounter(new() { ["/a.cs"] = 3 });

        ChurnMetrics metric = GitChurnCalculator.Calculate(
            [Method("N.A", "/a.cs", 0.1234), Method("N.A", "/a.cs", 0.0005, "Y")], counter).Single();

        Assert.That(metric.Score, Is.EqualTo(0.124));
        Assert.That(metric.Churn, Is.EqualTo(0.372));
    }

    [Test]
    public void Calculate_WithoutMethodsReturnsNothingAndNeverCallsGit()
    {
        var counter = new FakeCounter([]);

        Assert.That(GitChurnCalculator.Calculate([], counter), Is.Empty);
        Assert.That(counter.Calls, Is.Empty);
    }

    [Test]
    public void Calculate_PropagatesCounterFailures()
    {
        var counter = new ThrowingCounter();

        Assert.Throws<ChurnException>(() => GitChurnCalculator.Calculate([Method("N.A", "/a.cs", 1)], counter));
    }

    private sealed class ThrowingCounter : IChangeCounter
    {
        public int Count(string file) => throw new ChurnException("boom");
    }

    [Test]
    public void Calculate_WithCoverageAddsCoverageRiskChurnAndRiskLevel()
    {
        var counter = new FakeCounter(new() { ["/a.cs"] = 8 });
        var coverage = new ClassCoverageIndex([ClassCoverage("N.A", 1, 4)]);

        ChurnMetrics metric = GitChurnCalculator.Calculate([Method("N.A", "/a.cs", 5)], counter, coverage).Single();

        Assert.That(metric.Churn, Is.EqualTo(40));
        Assert.That(metric.Coverage, Is.EqualTo(0.25));
        Assert.That(metric.RiskChurn, Is.EqualTo(30));
        Assert.That(metric.RiskLevel, Is.EqualTo(ChurnRiskLevel.Critical));
    }

    [Test]
    public void Calculate_ClassAbsentFromCoverageIsNotAvailableInsteadOfZeroCoverage()
    {
        var counter = new FakeCounter(new() { ["/a.cs"] = 8, ["/b.cs"] = 8 });
        var coverage = new ClassCoverageIndex([ClassCoverage("N.A", 0, 4)]);

        List<ChurnMetrics> result = GitChurnCalculator.Calculate(
            [Method("N.A", "/a.cs", 5), Method("N.B", "/b.cs", 1)], counter, coverage);

        ChurnMetrics absent = result.Single(metric => metric.ClassName == "N.B");
        Assert.That(absent.Coverage, Is.Null);
        Assert.That(absent.RiskChurn, Is.Null);
        Assert.That(absent.RiskLevel, Is.Null);
        ChurnMetrics uncovered = result.Single(metric => metric.ClassName == "N.A");
        Assert.That(uncovered.Coverage, Is.Zero);
        Assert.That(uncovered.RiskChurn, Is.EqualTo(40));
        Assert.That(uncovered.RiskLevel, Is.EqualTo(ChurnRiskLevel.Critical));
    }

    [TestCase(30.001, 0.499, ChurnRiskLevel.Critical)]
    [TestCase(30.0, 0.499, ChurnRiskLevel.High)]
    [TestCase(30.001, 0.5, ChurnRiskLevel.High)]
    [TestCase(20.001, 0.699, ChurnRiskLevel.High)]
    [TestCase(20.0, 0.699, ChurnRiskLevel.Medium)]
    [TestCase(20.001, 0.7, ChurnRiskLevel.Medium)]
    [TestCase(10.001, 0.799, ChurnRiskLevel.Medium)]
    [TestCase(10.0, 0.799, ChurnRiskLevel.Low)]
    [TestCase(10.001, 0.8, ChurnRiskLevel.Low)]
    [TestCase(100, 1.0, ChurnRiskLevel.Low)]
    [TestCase(0, 0, ChurnRiskLevel.Low)]
    public void RiskLevel_FollowsTheReferenceThresholdsAtTheirBoundaries(
        double churn, double coverage, ChurnRiskLevel expected)
    {
        Assert.That(GitChurnCalculator.RiskLevel(churn, coverage), Is.EqualTo(expected));
    }

    [Test]
    public void RiskChurn_IsChurnWeightedWithTheMissingCoverage()
    {
        Assert.That(GitChurnCalculator.RiskChurn(3, 5, 0.75), Is.EqualTo(3.75));
        Assert.That(GitChurnCalculator.RiskChurn(3, 5, 1), Is.Zero);
        Assert.That(GitChurnCalculator.RiskChurn(3, 5, 0), Is.EqualTo(15));
    }

    [Test]
    public void ClassCoverageIndex_UsesClassLevelEntriesWithLinesOnly()
    {
        var index = new ClassCoverageIndex(
        [
            new Coverage { FullyQualifiedClassName = "N.A", MethodName = "M", LinesCovered = 1, LinesTotal = 1 },
            ClassCoverage("N.NoLines", 0, 0),
            ClassCoverage("", 1, 1),
            ClassCoverage("N.A", 3, 4),
            ClassCoverage("N.A", 0, 4),
        ]);

        Assert.That(index.Rate("N.A"), Is.EqualTo(0.75), "first class-level entry wins");
        Assert.That(index.Rate("N.NoLines"), Is.Null);
        Assert.That(index.Rate("N.Unknown"), Is.Null);
        Assert.That(index.IsEmpty, Is.False);
        Assert.That(new ClassCoverageIndex([]).IsEmpty, Is.True);
    }

    [TestCase("Engine", "MyNs.Engine")]
    [TestCase("MyNs.Engine", "Engine")]
    public void ClassCoverageIndex_MatchesNamesThatDifferByANamespaceOnADotBoundary(string reported, string analysed)
    {
        var index = new ClassCoverageIndex([ClassCoverage(reported, 1, 2)]);

        Assert.That(index.Rate(analysed), Is.EqualTo(0.5));
    }

    [Test]
    public void ClassCoverageIndex_AmbiguousSuffixMatchIsNotAvailable()
    {
        var index = new ClassCoverageIndex([ClassCoverage("A.Engine", 1, 2), ClassCoverage("B.Engine", 2, 2)]);

        Assert.That(index.Rate("Engine"), Is.Null);
        Assert.That(index.Rate("A.Engine"), Is.EqualTo(0.5), "exact names stay exact");
        Assert.That(index.Rate("B.Engine"), Is.EqualTo(1));
    }

    [Test]
    public void ClassCoverageIndex_DoesNotMatchPartialNames()
    {
        var index = new ClassCoverageIndex([ClassCoverage("MyNs.SuperEngine", 1, 2)]);

        Assert.That(index.Rate("Engine"), Is.Null);
        Assert.That(index.Rate("MyNs.SuperEngine2"), Is.Null);
    }
}
