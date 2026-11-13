/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class CognitiveCodeAnalyserDetailTests
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

    private CognitiveMetrics Method(string body, CognitiveConfiguration? configuration = null, string name = "Run")
        => Analyse($$"""
            namespace X;
            public class Y
            {
                public void {{name}}(int a, int b)
                {
            {{body}}
                }
            }
            """, configuration).Single(m => m.MethodName == name);

    [Test]
    public void Counts_EveryConstruct_AndCyclomaticComplexity()
    {
        var m = Method("""
                    int x = 0, y = 1;
                    if (a > 0 && b > 0 || x == 1) { x++; } else { y++; }
                    for (int i = 0; i < 3; i++) { }
                    foreach (var z in new[] { 1 }) { }
                    while (x < 0) { }
                    do { } while (x < 0);
                    switch (a) { case 1: break; case 2: break; default: break; }
                    try { } catch (Exception) { } catch { } finally { }
                    var t = a > 0 ? 1 : 2;
                    return;
            """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(m.ifCount, Is.EqualTo(1));
            Assert.That(m.elseCount, Is.EqualTo(1));
            Assert.That(m.loopCount, Is.EqualTo(4));
            Assert.That(m.switchCount, Is.EqualTo(1));
            Assert.That(m.tryCatchCount, Is.EqualTo(1));
            Assert.That(m.returnCount, Is.EqualTo(1));
            Assert.That(m.argumentCount, Is.EqualTo(2));
            // x, y, i, the foreach variable z, t
            Assert.That(m.localVariableCount, Is.EqualTo(5));
            // 1 + if + 4 loops + 3 switch sections + 2 catches + ternary + (&& and ||)
            Assert.That(m.cyclomaticComplexity, Is.EqualTo(1 + 1 + 4 + 3 + 2 + 1 + 2));
        }
    }

    [Test]
    public void EmptyMethod_HasBaselineValues()
    {
        var m = Method("");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(m.cyclomaticComplexity, Is.EqualTo(1));
            Assert.That(m.nestingLevels, Is.EqualTo(0));
            Assert.That(m.ifCount + m.elseCount + m.loopCount + m.switchCount + m.tryCatchCount + m.returnCount, Is.EqualTo(0));
            Assert.That(m.localVariableCount, Is.EqualTo(0));
            Assert.That(m.Halstead, Is.Not.Null);
        }
    }

    [Test]
    public void BitwiseOperators_DoNotCountAsLogicalBranches()
    {
        var m = Method("var r = (a & b) | (a ^ b); var c = a + b;");

        Assert.That(m.cyclomaticComplexity, Is.EqualTo(1));
    }

    [Test]
    public void Metadata_NameClassSignatureLinesAndLineNumber()
    {
        var m = Analyse("""
            namespace A.B
            {
                public class Outer
                {
                    public class Inner
                    {
                        public static T Generic<T>(T value) where T : class
                        {
                            return value;
                        }
                    }
                }
            }
            """).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(m.MethodName, Is.EqualTo("Generic"));
            Assert.That(m.ClassName, Is.EqualTo("A.B.Outer.Inner"));
            Assert.That(m.methodSignature, Is.EqualTo("public static T Generic<T>(T value) where T : class"));
            Assert.That(m.methodLineNumber, Is.EqualTo(7));
            Assert.That(m.linesOfCode, Is.EqualTo(4));
            Assert.That(m.FilePath, Does.EndWith("Sample.cs"));
        }
    }

    [Test]
    public void FileScopedNamespace_AndGlobalClass_AreQualifiedAccordingly()
    {
        var scoped = Analyse("namespace Ns.Deep; public class C { public void M() { } }").Single();
        var global = Analyse("public class G { public void M() { } }").Single();

        Assert.That(scoped.ClassName, Is.EqualTo("Ns.Deep.C"));
        Assert.That(global.ClassName, Is.EqualTo("G"));
    }

    [Test]
    public void MethodWithoutTypeParametersOrConstraints_HasPlainSignature()
    {
        var m = Analyse("public class C { void M(int a) { } }").Single();

        Assert.That(m.methodSignature, Is.EqualTo(" void M(int a)").Or.EqualTo("void M(int a)"));
    }

    [Test]
    public void FieldAndPropertyAccesses_CountQualifiedAndUnqualifiedButNotMethodsOrLocals()
    {
        var m = Analyse("""
            public class C
            {
                private int _f;
                public int P { get; set; }
                public string Name = "";
                public void M(C other)
                {
                    _f = 1;
                    this._f = 2;
                    P = 3;
                    other.P = 4;
                    var l = Name.Length;
                    var s = other?.Name;
                    var local = 5;
                    local++;
                    ToString();
                }
            }
            """).Single();

        // fields: _f, this._f, Name, Name (via other?.Name binding is skipped) -> 3 ; properties: P, other.P, Name.Length -> 3
        using (Assert.EnterMultipleScope())
        {
            Assert.That(m.fieldAccessCount, Is.EqualTo(3));
            Assert.That(m.propertyAccessCount, Is.EqualTo(3));
        }
    }

    [Test]
    public void Nesting_NestedIfs_AccumulateAndSiblingsDoNot()
    {
        Assert.That(Method("if (a > 0) { if (b > 0) { if (a > b) { } } }").nestingLevels, Is.EqualTo(3));
        Assert.That(Method("if (a > 0) { } if (b > 0) { }").nestingLevels, Is.EqualTo(1));
        Assert.That(Method("if (a > 0) { }").nestingLevels, Is.EqualTo(1));
    }

    [Test]
    public void Nesting_ElseIfChain_IsFlatByDefault_AndDeepensWhenConfigured()
    {
        const string chain = "if (a > 0) { } else if (a < 0) { } else if (b > 0) { } else { }";

        Assert.That(Method(chain).nestingLevels, Is.EqualTo(1));
        Assert.That(Method(chain, new CognitiveConfiguration { CountElseIfAsNesting = true }).nestingLevels, Is.EqualTo(3));
    }

    [Test]
    public void Nesting_ElseCountsAsLevelOnlyWhenConfigured()
    {
        const string body = "if (a > 0) { } else { }";

        Assert.That(Method(body).nestingLevels, Is.EqualTo(1));
        Assert.That(Method(body, new CognitiveConfiguration { CountElseAsNesting = true }).nestingLevels, Is.EqualTo(2));
    }

    [Test]
    public void Nesting_IfInsideElse_WithElseCountedAsNesting()
    {
        const string body = "if (a > 0) { } else { if (b > 0) { } }";

        Assert.That(Method(body).nestingLevels, Is.EqualTo(2));
        Assert.That(Method(body, new CognitiveConfiguration { CountElseAsNesting = true }).nestingLevels, Is.EqualTo(3));
    }

    [TestCase("for (int i = 0; i < a; i++) { }")]
    [TestCase("foreach (var i in new[] { 1 }) { }")]
    [TestCase("while (a > 0) { }")]
    [TestCase("do { } while (a > 0);")]
    [TestCase("switch (a) { default: break; }")]
    [TestCase("try { } catch { }")]
    [TestCase("lock (this) { }")]
    [TestCase("using (var d = new System.IO.MemoryStream()) { }")]
    [TestCase("checked { }")]
    [TestCase("unsafe { }")]
    public void Nesting_StructuredStatements_CountAsOneLevel(string body)
    {
        Assert.That(Method(body).nestingLevels, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void Nesting_FixedAndFinally_CountAsLevel()
    {
        Assert.That(Method("try { } finally { }").nestingLevels, Is.EqualTo(1));
        Assert.That(
            Analyse("public class C { public unsafe void M(int[] a) { fixed (int* p = a) { } } }").Single().nestingLevels,
            Is.EqualTo(1));
    }

    [Test]
    public void Nesting_StructuredStatements_AccumulateLikeIfs()
    {
        Assert.That(Method("for (;;) { for (;;) { for (;;) { } } }").nestingLevels, Is.EqualTo(3));
        Assert.That(Method("if (a > 0) { for (;;) { } }").nestingLevels, Is.EqualTo(2));
        Assert.That(Method("for (;;) { if (a > 0) { } }").nestingLevels, Is.EqualTo(2));
    }

    [TestCase("while (a > 0) { while (b > 0) { if (a > b) { } } }", 3)]
    [TestCase("foreach (var x in new[] { 1 }) { do { switch (a) { case 1: break; } } while (a > 0); }", 3)]
    [TestCase("try { lock (this) { using (var d = new System.IO.MemoryStream()) { } } } catch { }", 3)]
    [TestCase("switch (a) { case 1: if (b > 0) { } break; }", 2)]
    [TestCase("for (;;) { } for (;;) { }", 1)]
    [TestCase("for (;;) { } if (a > 0) { }", 1)]
    public void Nesting_MixedStructures_UsesDeepestPath(string body, int expected)
    {
        Assert.That(Method(body).nestingLevels, Is.EqualTo(expected));
    }

    [Test]
    public void Nesting_CatchAndFinallyBodies_ShareTheTryLevel()
    {
        Assert.That(Method("try { } catch { for (;;) { } }").nestingLevels, Is.EqualTo(2));
        Assert.That(Method("try { } finally { for (;;) { } }").nestingLevels, Is.EqualTo(2));
        Assert.That(Method("try { } catch { }").nestingLevels, Is.EqualTo(1));
        Assert.That(Method("try { for (;;) { } } catch { }").nestingLevels, Is.EqualTo(2));
    }

    [Test]
    public void Nesting_ElseFlags_ApplyToLoopsInsideElse()
    {
        const string body = "if (a > 0) { } else { for (;;) { } }";

        Assert.That(Method(body).nestingLevels, Is.EqualTo(2));
        Assert.That(Method(body, new CognitiveConfiguration { CountElseAsNesting = true }).nestingLevels, Is.EqualTo(3));
        Assert.That(Method(body, new CognitiveConfiguration { CountElseIfAsNesting = true }).nestingLevels, Is.EqualTo(2));
    }

    [Test]
    public void Nesting_ElseIfWithLoop_HonoursCountElseIfAsNesting()
    {
        const string body = "if (a > 0) { } else if (b > 0) { for (;;) { } }";

        Assert.That(Method(body).nestingLevels, Is.EqualTo(2));
        Assert.That(Method(body, new CognitiveConfiguration { CountElseIfAsNesting = true }).nestingLevels, Is.EqualTo(3));
    }

    [Test]
    public void LocalVariables_ForeachAndCatchVariables_AreCounted()
    {
        var m = Method("""
                    foreach (var item in new[] { 1, 2 }) { }
                    try { } catch (Exception ex) { }
                    try { } catch (Exception) { }
                    try { } catch { }
            """);

        Assert.That(m.localVariableCount, Is.EqualTo(2));
    }

    [Test]
    public void DeconstructingForeach_CountsAsLoopNestingAndLocals()
    {
        var m = Method("foreach (var (k, v) in new System.Collections.Generic.Dictionary<int, int>()) { foreach (var x in new[] { 1 }) { } }");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(m.loopCount, Is.EqualTo(2));
            Assert.That(m.nestingLevels, Is.EqualTo(2));
            Assert.That(m.localVariableCount, Is.EqualTo(3));
            Assert.That(m.cyclomaticComplexity, Is.EqualTo(3));
        }
    }

    [Test]
    public void DeconstructingForeach_ReviewExample_HasNestingLoopsAndLocalsOfTwo()
    {
        var m = Method("foreach (var (k, v) in d) { }");

        Assert.That((m.loopCount, m.nestingLevels, m.localVariableCount), Is.EqualTo((1, 1, 2)));
    }

    [TestCase("foreach ((int a, int b) in d) { }", 2)]
    [TestCase("foreach (var (a, _) in d) { }", 1)]
    [TestCase("foreach (var (a, (b, c)) in d) { }", 3)]
    [TestCase("foreach (var (_, _) in d) { }", 0)]
    public void DeconstructingForeach_CountsNamedVariablesOnly(string body, int locals)
    {
        Assert.That(Method(body).localVariableCount, Is.EqualTo(locals));
    }

    [Test]
    public void LocalVariables_ForLoopAndDeclarations_AreNotCountedTwice()
    {
        var m = Method("for (int i = 0; i < 3; i++) { } int x = 0;");

        Assert.That(m.localVariableCount, Is.EqualTo(2));
    }

    [Test]
    public void Nesting_ExpressionBodiedMethod_IsZero()
    {
        var m = Analyse("public class C { public int M(int a) => a > 0 ? 1 : 2; }").Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(m.nestingLevels, Is.EqualTo(0));
            Assert.That(m.cyclomaticComplexity, Is.EqualTo(2));
        }
    }

    [Test]
    public void AnalyseFiles_MultipleFiles_AggregatesAllMethods()
    {
        var f1 = _temp.CreateFileWithContent("One.cs", "public class One { public void A() { } }");
        var f2 = _temp.CreateFileWithContent("Two.cs", "public class Two { public void B() { } public void C() { } }");

        var result = new CognitiveCodeAnalyser().AnalyseFiles([f1, f2], new CognitiveConfiguration());

        Assert.That(result.Select(m => m.MethodName).OrderBy(n => n), Is.EqualTo(new[] { "A", "B", "C" }));
    }

    [Test]
    public void AnalyseFiles_ResultOrder_IsByFilePathThenLine_RegardlessOfParallelism()
    {
        var files = Enumerable.Range(0, 24)
            .Select(i => _temp.CreateFileWithContent($"F{i:D2}.cs", $"public class C{i} {{ public void B() {{ }}\npublic void A() {{ }} }}"))
            .Reverse()
            .ToList();

        for (int run = 0; run < 5; run++)
        {
            var result = new CognitiveCodeAnalyser().AnalyseFiles(files, new CognitiveConfiguration());

            Assert.That(
                result.Select(m => (m.FilePath, m.methodLineNumber)),
                Is.Ordered.Using<(string Path, int Line)>((x, y) =>
                    x.Path == y.Path ? x.Line.CompareTo(y.Line) : string.CompareOrdinal(x.Path, y.Path)));
            Assert.That(result.Select(m => m.MethodName).Take(2), Is.EqualTo(new[] { "B", "A" }));
        }
    }

    [Test]
    public void AnalyseFiles_ReportsProgressForEachFile()
    {
        var f1 = _temp.CreateFileWithContent("One.cs", "public class One { public void A() { } }");
        var f2 = _temp.CreateFileWithContent("Two.cs", "public class Two { public void B() { } }");
        var progress = new AnalysisProgressCollector();

        new CognitiveCodeAnalyser().AnalyseFiles([f1, f2], new CognitiveConfiguration(), progress);

        var reports = progress.Reports;
        Assert.That(reports.First().Phase, Is.EqualTo(AnalysisProgressPhase.AnalysingFiles));
        Assert.That(reports.First().ProcessedFiles, Is.EqualTo(0));
        Assert.That(reports.Count(r => r.Phase == AnalysisProgressPhase.AnalysingFiles), Is.EqualTo(3));
        Assert.That(reports.Last(), Is.EqualTo(new AnalysisProgress(AnalysisProgressPhase.AnalysisCompleted, 2, 2)));
    }

    [Test]
    public void AnalyseFilesAsync_Cancelled_Throws()
    {
        var f1 = _temp.CreateFileWithContent("One.cs", "public class One { public void A() { } }");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () =>
            await new CognitiveCodeAnalyser().AnalyseFilesAsync([f1], new CognitiveConfiguration(), cts.Token));
    }

    [Test]
    public async Task AnalyseFilesAsync_WithoutProgress_ReturnsMetrics()
    {
        var f1 = _temp.CreateFileWithContent("One.cs", "public class One { public void A() { } }");

        var result = await new CognitiveCodeAnalyser().AnalyseFilesAsync([f1], new CognitiveConfiguration());

        Assert.That(result, Has.Count.EqualTo(1));
    }
}
