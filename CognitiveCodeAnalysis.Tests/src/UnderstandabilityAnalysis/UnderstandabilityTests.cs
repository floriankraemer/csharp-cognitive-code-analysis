/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.UnderstandabilityAnalysis;

namespace CognitiveCodeAnalysis.Tests.UnderstandabilityAnalysis;

/// <summary>
/// Expected values follow the SonarSource white paper "Cognitive Complexity" by G. Ann Campbell.
/// The per-line increments of the white paper examples are commented in fixtures/understandability/WhitePaper.cs.
/// </summary>
public class UnderstandabilityTests
{
    private static string Fixture(string name)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", "understandability", name));

    private static string Describe(string fixture)
    {
        CognitiveMetricsCollection metrics = new CognitiveCodeAnalyser()
            .AnalyseFiles([Fixture(fixture)], new CognitiveConfiguration());

        return string.Concat(metrics.Select(m => m.MethodName + " " + Describe(m.Understandability) + "\n"));
    }

    private static string Describe(Understandability u)
    {
        var parts = new List<string> { "total=" + u.Total };
        void Add(string kind, int count)
        {
            if (count > 0)
            {
                parts.Add(kind + "=" + count);
            }
        }

        Add("structural", u.Structural);
        Add("hybrid", u.Hybrid);
        Add("fundamental", u.Fundamental);
        Add("nesting", u.Nesting);
        Add("recursion", u.Recursion);
        return string.Join(" ", parts);
    }

    [Test]
    public void ReproducesTheExamplesOfTheWhitePaper()
    {
        Assert.That(Describe("WhitePaper.cs"), Is.EqualTo("""
            SumOfPrimes total=7 structural=3 fundamental=1 nesting=3
            GetWords total=1 structural=1
            OverriddenSymbolFrom total=19 structural=7 hybrid=1 fundamental=1 nesting=10
            AddVersion total=35 structural=11 fundamental=2 nesting=22
            ToRegexp total=20 structural=5 hybrid=6 fundamental=3 nesting=6

            """.ReplaceLineEndings("\n")));
    }

    [Test]
    public void AppliesEachRuleOfTheSpecification()
    {
        Assert.That(Describe("Rules.cs"), Is.EqualTo("""
            Rules total=2 structural=2
            Empty total=0
            IfOnly total=1 structural=1
            IfElse total=2 structural=1 hybrid=1
            ElseIfChainIsNotNestedDeeper total=12 structural=5 hybrid=3 nesting=4
            ElseBodyNests total=4 structural=2 hybrid=1 nesting=1
            Ternary total=1 structural=1
            NestedTernary total=3 structural=2 nesting=1
            TernaryInConditionIsNotNested total=2 structural=2
            SwitchCountsOnceNotPerCase total=1 structural=1
            SwitchExpressionNestsItsArms total=3 structural=2 nesting=1
            WhenGuardCountsOnlyItsSequence total=2 structural=1 fundamental=1
            CaseGuardCountsOnlyItsSequence total=2 structural=1 fundamental=1
            PatternCombinatorsAreSequences total=2 fundamental=2
            OtherPatternsDoNotCount total=0
            Loops total=5 structural=5
            NestedLoops total=6 structural=3 nesting=3
            DoWhileBodyNestsButConditionDoesNot total=4 structural=2 fundamental=1 nesting=1
            TryAndFinallyDoNotCount total=5 structural=3 fundamental=1 nesting=1
            LockUsingCheckedAndUnsafeDoNotNest total=1 structural=1
            LambdaNests total=2 structural=1 nesting=1
            LambdaInLambdaNestsTwice total=3 structural=1 nesting=2
            LambdaInsideIfNests total=4 structural=2 nesting=2
            AnonymousMethodNests total=2 structural=1 nesting=1
            LocalFunctionsNest total=4 structural=2 nesting=2
            LikeOperators total=1 fundamental=1
            LikeOrOperators total=1 fundamental=1
            AndThenOr total=2 fundamental=2
            OrThenAnd total=2 fundamental=2
            MixedSequence total=3 fundamental=3
            ParenthesesDoNotBreakASequence total=1 fundamental=1
            NegationStartsANewSequence total=2 fundamental=2
            BitwiseOperatorsDoNotCount total=0
            NullCoalescingDoesNotCount total=0
            SequenceInACallArgumentIsItsOwn total=2 fundamental=2
            GotoCounts total=4 structural=1 fundamental=3
            BreakAndContinueDoNotCount total=5 structural=3 nesting=2
            RecursionCountsOnce total=2 structural=1 fundamental=1 recursion=1
            CallOnOtherObjectIsNoRecursion total=0
            RecursionInLambda total=1 fundamental=1 recursion=1
            AwaitDoesNotCountButAwaitForeachIsALoop total=1 structural=1
            QueryClausesDoNotCount total=1 fundamental=1
            HeadersAreMeasuredOnTheLevelOfTheirStatement total=17 structural=12 fundamental=4 nesting=1
            Run total=0
            Check total=0

            """.ReplaceLineEndings("\n")));
    }

    [Test]
    public void CountsCallsToTheOwnMethodWithoutReceiverOrThroughThisOrTheOwnTypeOnly()
    {
        Assert.That(Describe("Recursion.cs"), Is.EqualTo("""
            ClassName total=1 fundamental=1 recursion=1
            ThisReceiver total=1 fundamental=1 recursion=1
            CountsOnce total=1 fundamental=1 recursion=1
            Overload total=1 fundamental=1 recursion=1
            Overload total=0
            Generic total=1 fundamental=1 recursion=1
            ToString total=0
            OtherClassName total=0
            OtherObject total=0
            ConditionalAccess total=0
            InsideLocalFunction total=1 fundamental=1 recursion=1
            Recursion total=0
            InnerThis total=1 fundamental=1 recursion=1
            InnerClassName total=1 fundamental=1 recursion=1
            OuterClassName total=0

            """.ReplaceLineEndings("\n")));
    }

    [Test]
    public void TotalIsTheSumOfStructuralHybridFundamentalAndNesting()
    {
        var understandability = new Understandability(Structural: 2, Hybrid: 3, Fundamental: 4, Nesting: 5, Recursion: 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(understandability.Total, Is.EqualTo(14));
            Assert.That(understandability.RiskLevel, Is.EqualTo("high"));
            Assert.That(understandability.ToString(), Is.EqualTo("14 (high)"));
        }
    }

    [Test]
    public void MetricsWithoutMeasurementHaveNoComplexity()
    {
        var metrics = new CognitiveMetrics("m", "C", "f.cs", "void m()", 1);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metrics.Understandability, Is.SameAs(Understandability.None));
            Assert.That(metrics.Understandability.Total, Is.Zero);
            Assert.That(metrics.Understandability.ToString(), Is.EqualTo("0 (low)"));
        }
    }

    [TestCase(0, "low")]
    [TestCase(5, "low")]
    [TestCase(6, "medium")]
    [TestCase(10, "medium")]
    [TestCase(11, "high")]
    [TestCase(15, "high")]
    [TestCase(16, "very_high")]
    [TestCase(100, "very_high")]
    public void MapsComplexityToTheCyclomaticRiskBands(int complexity, string risk)
    {
        Assert.That(RiskLevel.Of(complexity), Is.EqualTo(risk));
    }
}
