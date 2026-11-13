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
    public void OldAbsolutePathOfAnotherCheckout_MatchesByUniqueClassAndSignature()
    {
        var baseline = Baseline(Method("/old/checkout/src/A.cs", "N.A", score: 1.0));

        Assert.That(DeltaOf(baseline, Method(Absolute("src/A.cs"), "N.A", score: 2.5)), Is.EqualTo(1.5));
    }

    [Test]
    public void SameMethodInTwoFilesOfAnotherCheckout_IsAmbiguousAndNotMatched()
    {
        var baseline = Baseline(
            Method("/old/src/A.cs", "N.A", score: 1.0),
            Method("/old/src/B.cs", "N.A", score: 10.0));

        Assert.That(DeltaOf(baseline, Method(Absolute("src/B.cs"), "N.A", score: 11.0)), Is.Null);
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
            Method("src/A.cs", "N.Holder.Inner"),
            Method("src/A.cs", "N.Other.Inner"));
        var current = new CognitiveMetricsCollection { Method("src/A.cs", "N.Holder.Other.Inner") };

        var comparison = BaselineComparer.Compare(current, baseline);

        Assert.That(comparison.TryGetMethodComparison(current[0], out var method), Is.True);
        Assert.That(method!.HasBaseline, Is.False);
    }

    [Test]
    public void RenamedNestedClass_InAnotherFile_DoesNotMatch()
    {
        var baseline = Baseline(Method("src/A.cs", "N.Inner"));

        Assert.That(DeltaOf(baseline, Method("src/Other.cs", "N.Holder.Inner")), Is.Null);
    }

    [TestCase("N.Inner", "N.Holder.Inner", true)]
    [TestCase("Inner", "N.Inner", true)]
    [TestCase("Inner", "N.Holder.Inner", false)]
    [TestCase("N.Holder.Inner", "N.Holder.Inner", false)]
    [TestCase("N.Holder.Inner", "N.Inner", false)]
    [TestCase("N.Other", "N.Holder.Inner", false)]
    [TestCase("M.Inner", "N.Holder.Inner", false)]
    [TestCase("N.Inner", "N.Holder.Other", false)]
    [TestCase("Holder.N", "N.Holder.Inner", false)]
    public void IsRenamedFrom_ComparesTheNameChains(string older, string current, bool expected) =>
        Assert.That(BaselineMatcher.IsRenamedFrom(older, current), Is.EqualTo(expected));

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

    [Test]
    public void SameMethodInTwoFiles_OnlyTheExactFileIsMatched_NotTheOtherByFallback()
    {
        var baseline = Baseline(Method("src/A.cs", "N1.Util", "int M(int x)", score: 1.0));
        var inA = Method("src/A.cs", "N1.Util", "int M(int x)", score: 2.0);
        var inC = Method("src/C.cs", "N1.Util", "int M(int x)", score: 9.0);
        var current = new CognitiveMetricsCollection { inC, inA };

        var comparison = BaselineComparer.Compare(current, baseline);

        Assert.That(comparison.TryGetMethodComparison(inA, out var a), Is.True);
        Assert.That(comparison.TryGetMethodComparison(inC, out var c), Is.True);
        Assert.That(a!.TotalScore.Delta, Is.EqualTo(1.0));
        Assert.That(c!.HasBaseline, Is.False);
    }

    [Test]
    public void TwoCurrentMethodsForOneUnclaimedBaselineEntry_NeitherIsMatched()
    {
        var baseline = Baseline(Method("/old/Gone.cs", "N.A"));
        var one = Method("src/X.cs", "N.A");
        var two = Method("src/Y.cs", "N.A");

        var comparison = BaselineComparer.Compare(new CognitiveMetricsCollection { one, two }, baseline);

        Assert.That(comparison.TryGetMethodComparison(one, out var first), Is.True);
        Assert.That(comparison.TryGetMethodComparison(two, out var second), Is.True);
        Assert.That(first!.HasBaseline || second!.HasBaseline, Is.False);
    }

    [Test]
    public void BaselinePathOfAnotherCheckout_IsNotMatchedByAPathSuffix()
    {
        // Same class and signature exist twice in the baseline, so only the full path could tell them apart.
        var baseline = Baseline(
            Method("/old/checkout/src/A.cs", "N.A", score: 1.0),
            Method("/old/checkout/other/src/A.cs", "N.A", score: 7.0));

        Assert.That(DeltaOf(baseline, Method(Absolute("src/A.cs"), "N.A", score: 2.0)), Is.Null);
    }

    private static CognitiveMetricsCollection WithCoupling(string className, string file, int incoming)
    {
        var collection = new CognitiveMetricsCollection { Method(file, className) };
        collection.SetClassCouplingMetrics([new ClassCouplingMetrics { ClassName = className, IncomingCoupling = incoming }]);
        return collection;
    }

    [Test]
    public void ClassCoupling_RenameIsNotMatchedWhenTheOldNameStillExistsInTheRun()
    {
        var baseline = BaselineSnapshotFactory.FromMetricsCollection(WithCoupling("Acme.Foo", "src/A.cs", 1));
        var current = WithCoupling("Acme.Sub.Foo", "src/A.cs", 5);
        current.Add(Method("src/B.cs", "Acme.Foo"));
        current.SetClassCouplingMetrics(
        [
            new ClassCouplingMetrics { ClassName = "Acme.Sub.Foo", IncomingCoupling = 5 },
            new ClassCouplingMetrics { ClassName = "Acme.Foo", IncomingCoupling = 1 },
        ]);

        var comparison = BaselineComparer.Compare(current, baseline);

        Assert.That(comparison.TryGetClassCouplingComparison("Acme.Sub.Foo", out var renamed), Is.True);
        Assert.That(renamed!.Baseline, Is.Null);
        Assert.That(comparison.TryGetClassCouplingComparison("Acme.Foo", out var same), Is.True);
        Assert.That(same!.Baseline, Is.Not.Null);
    }

    [Test]
    public void ClassCoupling_RenameInAnotherFile_IsNotMatched()
    {
        var baseline = BaselineSnapshotFactory.FromMetricsCollection(WithCoupling("Acme.Foo", "src/A.cs", 1));

        var comparison = BaselineComparer.Compare(WithCoupling("Acme.Sub.Foo", "src/Z.cs", 5), baseline);

        Assert.That(comparison.TryGetClassCouplingComparison("Acme.Sub.Foo", out var coupling), Is.True);
        Assert.That(coupling!.Baseline, Is.Null);
    }

    [Test]
    public void ClassCoupling_RenameWithTwoInsertedSegments_IsNotMatched()
    {
        var baseline = BaselineSnapshotFactory.FromMetricsCollection(WithCoupling("Acme.Foo", "src/A.cs", 1));

        var comparison = BaselineComparer.Compare(WithCoupling("Acme.A.B.Foo", "src/A.cs", 5), baseline);

        Assert.That(comparison.TryGetClassCouplingComparison("Acme.A.B.Foo", out var coupling), Is.True);
        Assert.That(coupling!.Baseline, Is.Null);
    }
}
