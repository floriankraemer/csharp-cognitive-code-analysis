/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Baseline;

public class BaselineMatchingTests
{
    private static readonly string WorkingDirectory = Directory.GetCurrentDirectory();

    private static string Absolute(string relative) => Path.Combine(WorkingDirectory, relative).Replace('\\', '/');

    private static CognitiveMetrics Method(string file, string className, string signature = "void Run()", double score = 1.0)
    {
        var metrics = new CognitiveMetrics("Run", className, file, signature, 1) { totalScore = score };
        return metrics;
    }

    private static CognitiveBaselineSnapshot Baseline(params CognitiveMetrics[] methods) =>
        BaselineSnapshotFactory.FromMetricsCollection([.. methods]);

    private static double? DeltaOf(CognitiveBaselineSnapshot baseline, CognitiveMetrics current)
    {
        var comparison = BaselineComparer.Compare([current], baseline);
        Assert.That(comparison.TryGetMethodComparison(current, out var method), Is.True);
        return method!.HasBaseline ? method.TotalScore.Delta : null;
    }

    [Test]
    public void ExactKey_RelativeBaselinePathMatchesAbsoluteCurrentPath()
    {
        var baseline = Baseline(Method("src/A.cs", "N.A", score: 1.0));

        Assert.That(DeltaOf(baseline, Method(Absolute("src/A.cs"), "N.A", score: 3.0)), Is.EqualTo(2.0));
    }

    [Test]
    public void ExactKey_OldAbsoluteBaselinePathMatchesOnTheSameCheckout()
    {
        var baseline = Baseline(Method(Absolute("src/A.cs"), "N.A", score: 1.0));

        Assert.That(DeltaOf(baseline, Method(Absolute("src/A.cs"), "N.A", score: 4.0)), Is.EqualTo(3.0));
    }

    [Test]
    public void RelativePath_OldAbsolutePathOfAnotherCheckoutMatches()
    {
        var baseline = Baseline(Method("/old/checkout/src/A.cs", "N.A", score: 1.0));

        Assert.That(DeltaOf(baseline, Method(Absolute("src/A.cs"), "N.A", score: 2.5)), Is.EqualTo(1.5));
    }

    [Test]
    public void RelativePath_PicksTheFileWhenTheSameMethodIsInTwoFiles()
    {
        var baseline = Baseline(
            Method("/old/src/A.cs", "N.A", score: 1.0),
            Method("/old/src/B.cs", "N.A", score: 10.0));

        Assert.That(DeltaOf(baseline, Method(Absolute("src/B.cs"), "N.A", score: 11.0)), Is.EqualTo(1.0));
    }

    [Test]
    public void ClassAndSignature_MatchWhenTheFileMovedAndTheMethodIsUnique()
    {
        var baseline = Baseline(Method("/old/src/Old.cs", "N.A", score: 1.0));

        Assert.That(DeltaOf(baseline, Method(Absolute("src/New.cs"), "N.A", score: 2.0)), Is.EqualTo(1.0));
    }

    [Test]
    public void ClassAndSignature_AmbiguousAcrossTheBaseline_DoesNotMatch()
    {
        var baseline = Baseline(
            Method("/old/src/X.cs", "N.A"),
            Method("/old/src/Y.cs", "N.A"));

        Assert.That(DeltaOf(baseline, Method(Absolute("src/New.cs"), "N.A")), Is.Null);
    }

    [Test]
    public void ClassAndSignature_OtherSignatureOrClass_DoesNotMatch()
    {
        var baseline = Baseline(Method("/old/src/A.cs", "N.A", "void Run()"));

        Assert.That(DeltaOf(baseline, Method(Absolute("src/A.cs"), "N.A", "void Run(int x)")), Is.Null);
        Assert.That(DeltaOf(baseline, Method(Absolute("src/A.cs"), "N.B")), Is.Null);
    }

    [Test]
    public void RenamedNestedClass_MatchesWhenTheContainerWasAdded()
    {
        var baseline = Baseline(Method("src/A.cs", "N.Inner", score: 1.0));

        Assert.That(DeltaOf(baseline, Method("src/A.cs", "N.Holder.Inner", score: 2.0)), Is.EqualTo(1.0));
    }

    [Test]
    public void RenamedNestedClass_AmbiguousOlderNames_DoesNotMatch()
    {
        var baseline = Baseline(
            Method("src/A.cs", "N.Inner"),
            Method("src/A.cs", "Inner"));

        Assert.That(DeltaOf(baseline, Method("src/A.cs", "N.Holder.Inner")), Is.Null);
    }

    [Test]
    public void RenamedNestedClass_InAnotherFile_DoesNotMatch()
    {
        var baseline = Baseline(Method("src/A.cs", "N.Inner"));

        Assert.That(DeltaOf(baseline, Method("src/Other.cs", "N.Holder.Inner")), Is.Null);
    }

    [TestCase("N.Inner", "N.Holder.Inner", true)]
    [TestCase("Inner", "N.Holder.Inner", true)]
    [TestCase("N.Holder.Inner", "N.Holder.Inner", false)]
    [TestCase("N.Holder.Inner", "N.Inner", false)]
    [TestCase("N.Other", "N.Holder.Inner", false)]
    [TestCase("M.Inner", "N.Holder.Inner", false)]
    [TestCase("Holder.N", "N.Holder.Inner", false)]
    public void IsRenamedFrom_ComparesTheNameChains(string older, string current, bool expected) =>
        Assert.That(BaselineMethodIndex.IsRenamedFrom(older, current), Is.EqualTo(expected));

    [Test]
    public void DuplicateBaselineKeys_LastEntryWins()
    {
        var baseline = Baseline(Method("src/A.cs", "N.A", score: 0.5));
        baseline.Methods.Add(new CognitiveBaselineMethodSnapshot
        {
            FilePath = "src/A.cs", ClassName = "N.A", MethodSignature = "void Run()", TotalScore = 1.0,
        });

        Assert.That(DeltaOf(baseline, Method("src/A.cs", "N.A", score: 5.0)), Is.EqualTo(4.0));
    }

    [Test]
    public void ClassCoupling_MatchesTheRenamedNestedClass()
    {
        var older = new CognitiveMetricsCollection { Method("src/A.cs", "N.Inner") };
        older.SetClassCouplingMetrics([new ClassCouplingMetrics { ClassName = "N.Inner", IncomingCoupling = 1, OutgoingCoupling = 2, Stability = 0.5 }]);
        var baseline = BaselineSnapshotFactory.FromMetricsCollection(older);

        var current = new CognitiveMetricsCollection { Method("src/A.cs", "N.Holder.Inner") };
        current.SetClassCouplingMetrics([new ClassCouplingMetrics { ClassName = "N.Holder.Inner", IncomingCoupling = 4, OutgoingCoupling = 2, Stability = 0.5 }]);

        var comparison = BaselineComparer.Compare(current, baseline);

        Assert.That(comparison.TryGetClassCouplingComparison("N.Holder.Inner", out var coupling), Is.True);
        Assert.That(coupling!.IncomingCoupling.Delta, Is.EqualTo(3));
    }

    [Test]
    public void ClassCoupling_UnknownClass_HasNoDelta()
    {
        var current = new CognitiveMetricsCollection { Method("src/A.cs", "N.Fresh") };
        current.SetClassCouplingMetrics([new ClassCouplingMetrics { ClassName = "N.Fresh", IncomingCoupling = 4 }]);

        var comparison = BaselineComparer.Compare(current, Baseline());

        Assert.That(comparison.TryGetClassCouplingComparison("N.Fresh", out var coupling), Is.True);
        Assert.That(coupling!.Baseline, Is.Null);
    }

    [Test]
    public void Comparison_ExposesTheConfigHashOfTheBaseline()
    {
        var withHash = new CognitiveBaselineSnapshot { ConfigHash = "abc" };

        Assert.That(BaselineComparer.Compare([], withHash).BaselineConfigHash, Is.EqualTo("abc"));
        Assert.That(BaselineComparer.Compare([], Baseline()).BaselineConfigHash, Is.Null);
    }

    [Test]
    public void Snapshot_StoresRelativePathsOnlyWhenAsked()
    {
        var metrics = new CognitiveMetricsCollection { Method(Absolute("src/A.cs"), "N.A") };
        var now = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var relative = BaselineSnapshotFactory.FromMetricsCollection(metrics, relativePaths: true, configHash: "h", generatedAt: now);
        var absolute = BaselineSnapshotFactory.FromMetricsCollection(metrics, relativePaths: false, configHash: null, generatedAt: now);

        Assert.That(relative.Methods.Single().FilePath, Is.EqualTo("src/A.cs"));
        Assert.That(relative.ConfigHash, Is.EqualTo("h"));
        Assert.That(relative.GeneratedAt, Is.EqualTo(now));
        Assert.That(absolute.Methods.Single().FilePath, Is.EqualTo(Absolute("src/A.cs")));
        Assert.That(absolute.ConfigHash, Is.Null);
    }
}
