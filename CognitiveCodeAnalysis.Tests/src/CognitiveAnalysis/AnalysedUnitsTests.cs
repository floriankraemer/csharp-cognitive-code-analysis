/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

/// <summary>
/// Which declarations become rows: methods and constructors of classes, structs, records and interfaces,
/// minus those tagged with <c>@cca-ignore</c> or matched by <c>excludePatterns</c>.
/// </summary>
public class AnalysedUnitsTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private CognitiveMetricsCollection Analyse(string source, CognitiveConfiguration? configuration = null)
    {
        var file = _temp.CreateFileWithContent("Sample.cs", source);
        return new CognitiveCodeAnalyser().AnalyseFiles([file], configuration ?? new CognitiveConfiguration());
    }

    private static string[] Units(CognitiveMetricsCollection metrics) =>
        metrics.Select(m => $"{m.ClassName}::{m.MethodName}").ToArray();

    [TestCase("public class T", "public void Run(int a)", "Run", "public void Run(int a)", TestName = "Class method")]
    [TestCase("public struct T", "public void Run(int a)", "Run", "public void Run(int a)", TestName = "Struct method")]
    [TestCase("public record T(int X)", "public void Run(int a)", "Run", "public void Run(int a)", TestName = "Record method")]
    [TestCase("public record struct T(int X)", "public void Run(int a)", "Run", "public void Run(int a)", TestName = "Record struct method")]
    [TestCase("public interface T", "void Run(int a)", "Run", " void Run(int a)", TestName = "Default interface method")]
    [TestCase("public class T", "public T(int a)", "T", "public T(int a)", TestName = "Class constructor")]
    [TestCase("public struct T", "public T(int a)", "T", "public T(int a)", TestName = "Struct constructor")]
    [TestCase("public record T", "public T(int a)", "T", "public T(int a)", TestName = "Record constructor")]
    public void EveryUnitKind_IsMeasuredLikeAClassMethod(string typeHeader, string declaration, string name, string signature)
    {
        var m = Analyse($$"""
            namespace N;
            {{typeHeader}}
            {
                {{declaration}}
                {
                    if (a > 0)
                    {
                        for (int i = 0; i < a; i++) { a--; }
                        return;
                    }
                }
            }
            """).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(m.ClassName, Is.EqualTo("N.T"));
            Assert.That(m.MethodName, Is.EqualTo(name));
            Assert.That(m.methodSignature, Is.EqualTo(signature));
            Assert.That(m.methodLineNumber, Is.EqualTo(4));
            Assert.That(m.linesOfCode, Is.EqualTo(8));
            Assert.That(m.argumentCount, Is.EqualTo(1));
            Assert.That(m.ifCount, Is.EqualTo(1));
            Assert.That(m.loopCount, Is.EqualTo(1));
            Assert.That(m.returnCount, Is.EqualTo(1));
            Assert.That(m.localVariableCount, Is.EqualTo(1));
            Assert.That(m.nestingLevels, Is.EqualTo(2));
            Assert.That(m.cyclomaticComplexity, Is.EqualTo(3));
            Assert.That(m.Halstead!.Identifier, Is.EqualTo("N.T::" + name));
            Assert.That(m.Halstead!.TotalOperators, Is.GreaterThan(0));
        }
    }

    [Test]
    public void MethodsAndConstructors_AreTheOnlyUnits()
    {
        var all = Analyse("""
            namespace N;
            public class C
            {
                public C() { }
                static C() { }
                ~C() { }
                public int P { get => 1; set { } }
                public int this[int i] => i;
                public event System.Action E { add { } remove { } }
                public static C operator +(C a, C b) => a;
                public static implicit operator int(C c) => 0;
                public void M() { }
                public abstract void Abstract();
            }
            public struct S { public S(int x) { } public void N() { } }
            public interface I { void O(); void D() { } int F() => 1; static void G() { } }
            public record R(int X) { public void Q() { } }
            public record struct RS(int X) { public void W() { } }
            public enum En { A }
            public delegate void Del();
            """);

        Assert.That(all.Select(m => $"{m.ClassName}::{m.MethodName}|{m.methodSignature}"), Is.EquivalentTo(new[]
        {
            "N.C::C|public C()",
            "N.C::C|static C()",
            "N.C::M|public void M()",
            "N.C::Abstract|public abstract void Abstract()",
            "N.S::S|public S(int x)",
            "N.S::N|public void N()",
            "N.I::D| void D()",
            "N.I::F| int F()",
            "N.I::G|static void G()",
            "N.R::Q|public void Q()",
            "N.RS::W|public void W()",
        }));
    }

    [Test]
    public void ConstructorSignature_LeavesOutTheInitializer_AndHasNoLeadingSpaceWithoutModifiers()
    {
        var all = Analyse("""
            public class C { public C(int a) : this() { } internal C() { } }
            class D { D() { } }
            """);

        Assert.That(all.Select(m => m.methodSignature), Is.EquivalentTo(new[] { "public C(int a)", "internal C()", "D()" }));
    }

    [Test]
    public void ConstructorInitializer_IsPartOfTheConstructor()
    {
        var m = Analyse("public class C { public C(int a) : this(a > 0 ? 1 : 2, a) { } public C(int a, int b) { } }")
            .First(c => c.argumentCount == 1);

        Assert.That(m.cyclomaticComplexity, Is.EqualTo(2));
    }

    [Test]
    public void LocalFunctionsAndLambdas_BelongToTheirMethod()
    {
        var m = Analyse("""
            public class C
            {
                public int M(int a)
                {
                    System.Func<int, int> f = x => x > 0 ? x : -x;
                    return Local(a);

                    int Local(int b)
                    {
                        if (b > 0) { return f(b); }
                        return 0;
                    }
                }
            }
            """).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(m.MethodName, Is.EqualTo("M"));
            Assert.That(m.ifCount, Is.EqualTo(1));
            Assert.That(m.returnCount, Is.EqualTo(3));
            Assert.That(m.cyclomaticComplexity, Is.EqualTo(3));
        }
    }

    [Test]
    public void NestedTypes_AreNamedThroughEveryContainingType()
    {
        var all = Analyse("""
            namespace N;
            public struct S { public class Inner { public void M() { } } }
            public record R { public interface I { void D() { } } }
            public class O { public class Mid { public record struct Leaf { public void L() { } } } }
            """);

        Assert.That(Units(all), Is.EquivalentTo(new[] { "N.S.Inner::M", "N.R.I::D", "N.O.Mid.Leaf::L" }));
    }

    [Test]
    public void CcaIgnore_LeavesOutTaggedTypesAndMembers()
    {
        var all = Analyse("""
            namespace N;

            /// <summary>Generated.</summary>
            /// <remarks>@cca-ignore</remarks>
            public class IgnoredByXmlDoc
            {
                public void Hidden() { }

                public class NestedOfIgnored
                {
                    public void AlsoHidden() { }
                }
            }

            // @cca-ignore: generated code
            public struct IgnoredByLineComment
            {
                public void Hidden() { }
            }

            public record IgnoredByTrailingComment { public void Hidden() { } } // @cca-ignore

            public class PartiallyIgnored
            {
                /// <summary>
                /// Does something.
                /// @cca-ignore
                /// </summary>
                public void IgnoredByXmlDoc() { }

                /// @cca-ignore
                public void IgnoredByPlainDocComment() { }

                /** @cca-ignore */
                public void IgnoredByMultiLineDocComment() { }

                // @cca-ignore
                public void IgnoredByLineComment() { }

                // @cca-ignore because of reasons
                /// <summary>Doc closer to the method.</summary>
                public void IgnoredByStackedComment() { }

                [System.Obsolete]
                /* @cca-ignore */
                public void IgnoredBetweenAttributeAndDeclaration() { }

                public /* @cca-ignore */ void IgnoredBetweenModifierAndName() { }

                public void IgnoredByTrailingComment() { } // @cca-ignore

                public void KeptAfterTrailingComment() { }

                public void Kept()
                {
                    // @cca-ignore inside the body does not ignore the method
                }

                [System.ComponentModel.Description("@cca-ignore")]
                public void KeptWithTagInAttributeArgument() { }

                // @CCA-IGNORE is case-sensitive
                public void KeptWithUpperCaseTag() { }

                /* block comment @cca-ignore */
                public PartiallyIgnored() { }

                /// <summary>@cca-ignore</summary>
                public record IgnoredNested
                {
                    public void Hidden() { }
                }

                // a comment without the tag
                public interface KeptNested
                {
                    void Kept() { }
                }
            }
            """);

        Assert.That(Units(all), Is.EquivalentTo(new[]
        {
            "N.PartiallyIgnored::KeptAfterTrailingComment",
            "N.PartiallyIgnored::Kept",
            "N.PartiallyIgnored::KeptWithTagInAttributeArgument",
            "N.PartiallyIgnored::KeptWithUpperCaseTag",
            "N.PartiallyIgnored.KeptNested::Kept",
        }));
    }

    [Test]
    public void CcaIgnore_BetweenTwoDeclarationsOnOneLine_BelongsToTheFollowingDeclaration()
    {
        var all = Analyse("""
            public class C { public void A() { } /* @cca-ignore */ public void B() { } }
            public class D { } /* @cca-ignore */ public class E { public void M() { } }
            public class O { public class P { public void M() { } } /* @cca-ignore */ public class Q { public void M() { } } }
            public class F { public void Hidden() { } } // @cca-ignore
            public class G { public void Hidden() { } /* @cca-ignore */ }
            """);

        Assert.That(Units(all), Is.EquivalentTo(new[] { "C::A", "O.P::M" }));
    }

    [Test]
    public void CcaIgnore_AfterAnOpeningBrace_BelongsToTheFirstMember()
    {
        var all = Analyse("""
            public interface J { /* @cca-ignore */ void M1() { } void M2() { } }
            public class K
            { // @cca-ignore
                public void M1() { }
                public void M2() { }
            }
            public class L
            {
                public void M1() { } // @cca-ignore
                public void M2() { }
            }
            """);

        Assert.That(Units(all), Is.EquivalentTo(new[] { "J::M2", "K::M2", "L::M2" }));
    }

    [Test]
    public void CcaIgnore_AtTheEndOfAFieldLine_BelongsToTheField()
    {
        var all = Analyse("""
            public class C
            {
                private int _a; // @cca-ignore
                public void M() { }
                private int _b; /* @cca-ignore */ public void N() { }
            }
            """);

        Assert.That(Units(all), Is.EqualTo(new[] { "C::M" }));
    }

    [Test]
    public void ExcludePatterns_LeaveOutMatchingMethods_WithFindSemantics()
    {
        var configuration = new CognitiveConfiguration { ExcludePatterns = ["::Get", @"^N\.Legacy\."] };

        var all = Analyse("""
            namespace N
            {
                public class C
                {
                    public C() { }
                    public int GetA() => 1;
                    public int GetA(int x) => x;
                    public void Run() { }
                    public void ForgetIt() { }
                }
            }
            namespace N.Legacy { public class D { public void Run() { } } }
            namespace M.N.Legacy { public class E { public void Run() { } } }
            """, configuration);

        Assert.That(Units(all), Is.EquivalentTo(new[] { "N.C::C", "N.C::Run", "N.C::ForgetIt", "M.N.Legacy.E::Run" }));
    }

    [Test]
    public void ExcludePatterns_CanTargetConstructors()
    {
        var configuration = new CognitiveConfiguration { ExcludePatterns = [@"\.(\w+)::\1$"] };

        var all = Analyse("namespace N; public class C { public C() { } public void Run() { } }", configuration);

        Assert.That(Units(all), Is.EqualTo(new[] { "N.C::Run" }));
    }

    [Test]
    public void ExcludePatterns_AreCaseSensitive()
    {
        var configuration = new CognitiveConfiguration { ExcludePatterns = ["::run$"] };

        var all = Analyse("public class C { public void Run() { } }", configuration);

        Assert.That(Units(all), Is.EqualTo(new[] { "C::Run" }));
    }
}
