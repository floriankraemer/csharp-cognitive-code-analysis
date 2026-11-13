/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

/// <summary>
/// Coverlet reports the compiler-generated types of async methods, iterators, closures and local functions as classes
/// of their own. Their lines are merged into the method they were written in.
/// </summary>
public class GeneratedTypeCoverageTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private static string Fixture(string name)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", "coverlet", name));

    private (string Coverage, string Source) ArrangeFixture()
    {
        string source = Path.Combine(_temp.tmpDirectory, "Worker.cs");
        File.Copy(Fixture("Worker.cs"), source);
        string xml = File.ReadAllText(Fixture("generated-types.coverlet.cobertura.xml"))
            .Replace("__SOURCE_DIR__", _temp.tmpDirectory, StringComparison.Ordinal);
        return (_temp.CreateFileWithContent("coverage.xml", xml), source);
    }

    private List<Coverage> ReadInline(string classesXml)
        => new CoberturaReader().ReadCoverage(_temp.CreateFileWithContent("c.xml",
            $"""<coverage><sources><source>{_temp.tmpDirectory}</source></sources><packages><package name="Asm"><classes>{classesXml}</classes></package></packages></coverage>""")).ToList();

    private static string Method(string name, string lines, string attributes = """line-rate="1" branch-rate="1" """)
        => $"""<method name="{System.Security.SecurityElement.Escape(name)}" {attributes}><lines>{lines}</lines></method>""";

    // ---- real coverlet output ----

    [Test]
    public void Reader_MergesStateMachinesAndClosureHoldersIntoTheirMethods()
    {
        var (coverage, _) = ArrangeFixture();

        var list = new CoberturaReader().ReadCoverage(coverage).ToList();

        var methods = list.Where(c => c.IsMethodLevel).ToDictionary(c => c.MethodName);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(methods.Keys, Is.EquivalentTo(new[] { "LoadAsync", "Numbers", "Sum", "Plain" }));
            Assert.That(list.Where(c => !c.IsMethodLevel).Select(c => c.FullyQualifiedClassName), Is.EqualTo(new[] { "GenLib.Gen.Worker" }),
                "generated types are no classes of their own");

            Assert.That(methods["LoadAsync"].MethodLineNumber, Is.EqualTo(7));
            Assert.That((methods["LoadAsync"].LinesCovered, methods["LoadAsync"].LinesTotal), Is.EqualTo((4, 5)));
            Assert.That(methods["LoadAsync"].BranchCoveragePercentage, Is.EqualTo(50.0));

            Assert.That(methods["Numbers"].MethodLineNumber, Is.EqualTo(18));
            Assert.That(methods["Numbers"].LineCoveragePercentage, Is.EqualTo(100.0));
            Assert.That(methods["Numbers"].BranchCoveragePercentage, Is.EqualTo(100.0));

            // Sum keeps its own lines 26, 27, 33 and gains line 30 from the local function.
            Assert.That(methods["Sum"].MethodLineNumber, Is.EqualTo(26));
            Assert.That((methods["Sum"].LinesCovered, methods["Sum"].LinesTotal), Is.EqualTo((4, 4)));
            Assert.That(methods["Sum"].BranchCoveragePercentage, Is.Null);

            Assert.That(methods["Plain"].FromGeneratedTypes, Is.False);
            Assert.That(methods["Plain"].LinesTotal, Is.EqualTo(0), "ordinary methods keep their rates");
        }
    }

    [Test]
    public void Facade_GivesEveryMethodItsOwnCoverage_IncludingAsyncIteratorAndClosure()
    {
        var (coverage, source) = ArrangeFixture();
        var facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(), new CognitiveCodeAnalyser(), CognitiveConfigurationDefaults.Create(),
            new ScoreCalculator(), new CoberturaReader(), new ClassCouplingAnalyser());
        var metrics = facade.AnalyseSourceFiles([source]);

        var result = facade.LoadCoverageData(coverage, metrics);

        var byName = metrics.ToDictionary(m => m.MethodName);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.WarningMessage, Is.Null);
            Assert.That(byName["LoadAsync"].lineCoveragePercentage, Is.EqualTo(80.0), "the class-level 100% must not be used");
            Assert.That(byName["LoadAsync"].branchCoveragePercentage, Is.EqualTo(50.0));
            Assert.That(byName["Numbers"].lineCoveragePercentage, Is.EqualTo(100.0));
            Assert.That(byName["Numbers"].branchCoveragePercentage, Is.EqualTo(100.0));
            Assert.That(byName["Sum"].lineCoveragePercentage, Is.EqualTo(100.0));
            Assert.That(byName["Plain"].lineCoveragePercentage, Is.EqualTo(100.0));
        }
    }

    // ---- name mapping ----

    [Test]
    public void LambdaHolder_IsMappedByTheMethodNameInsideItsMemberName()
    {
        var list = ReadInline($"""
            <class name="N.C" filename="C.cs">
              <methods>{Method("Run", """<line number="10" hits="1" branch="False"/>""")}</methods>
            </class>
            <class name="N.C/&lt;&gt;c" filename="C.cs">
              <methods>{Method("<Run>b__0_0", """<line number="11" hits="0" branch="False"/>""")}</methods>
            </class>
            """);

        var run = list.Single(c => c.IsMethodLevel);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(run.MethodName, Is.EqualTo("Run"));
            Assert.That((run.LinesCovered, run.LinesTotal), Is.EqualTo((1, 2)));
            Assert.That(run.FromGeneratedTypes, Is.True);
        }
    }

    [Test]
    public void LocalFunctionInsideAnAsyncMethod_IsMappedToTheOutermostMethod()
    {
        var list = ReadInline($"""
            <class name="N.C/&lt;&lt;Load&gt;g__Inner|0&gt;d" filename="C.cs">
              <methods>{Method("MoveNext", """<line number="5" hits="1" branch="False"/>""")}</methods>
            </class>
            <class name="N.C/&lt;Load&gt;d__1" filename="C.cs">
              <methods>{Method("MoveNext", """<line number="7" hits="1" branch="False"/>""")}</methods>
            </class>
            """);

        // "<<Load>g__Inner|0>d" is no state machine name (no d__N suffix) and its holder method name is not usable.
        var load = list.Single(c => c.IsMethodLevel);
        Assert.That((load.MethodName, load.MethodLineNumber), Is.EqualTo(("Load", 7)));
    }

    [Test]
    public void IteratorStateMachine_WithExtraMethods_MergesAllOfThem()
    {
        var list = ReadInline($"""
            <class name="N.C/&lt;Items&gt;d__0" filename="C.cs">
              <methods>
                {Method("MoveNext", """<line number="5" hits="1" branch="False"/><line number="6" hits="1" branch="True" condition-coverage="50% (1/2)"/>""")}
                {Method("<>m__Finally1", """<line number="8" hits="0" branch="False"/>""")}
              </methods>
            </class>
            """);

        var items = list.Single(c => c.IsMethodLevel);
        using (Assert.EnterMultipleScope())
        {
            Assert.That((items.LinesCovered, items.LinesTotal), Is.EqualTo((2, 3)));
            Assert.That((items.BranchesCovered, items.BranchesTotal), Is.EqualTo((1, 2)));
            Assert.That(items.MethodLineNumber, Is.EqualTo(5));
        }
    }

    [Test]
    public void SameLineInOrdinaryAndGeneratedPart_IsCountedOnceWithTheHigherHits()
    {
        var list = ReadInline($"""
            <class name="N.C" filename="C.cs">
              <methods>{Method("Run", """<line number="10" hits="0" branch="False"/>""")}</methods>
            </class>
            <class name="N.C/&lt;&gt;c__DisplayClass0_0" filename="C.cs">
              <methods>{Method("<Run>b__0", """<line number="10" hits="3" branch="False"/>""")}</methods>
            </class>
            """);

        var run = list.Single(c => c.IsMethodLevel);
        Assert.That((run.LinesCovered, run.LinesTotal), Is.EqualTo((1, 1)));
    }

    [Test]
    public void Overloads_GetTheGeneratedPartThatStartsAfterTheirOwnFirstLine()
    {
        var list = ReadInline($"""
            <class name="N.C" filename="C.cs">
              <methods>
                {Method("Run", """<line number="10" hits="1" branch="False"/>""")}
                {Method("Run", """<line number="30" hits="1" branch="False"/>""")}
              </methods>
            </class>
            <class name="N.C/&lt;&gt;c__DisplayClass0_0" filename="C.cs">
              <methods>{Method("<Run>b__0", """<line number="32" hits="0" branch="False"/>""")}</methods>
            </class>
            """);

        var runs = list.Where(c => c.IsMethodLevel).OrderBy(c => c.MethodLineNumber).ToList();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(runs[0].FromGeneratedTypes, Is.False);
            Assert.That((runs[1].LinesCovered, runs[1].LinesTotal), Is.EqualTo((1, 2)));
        }
    }

    [Test]
    public void GeneratedPartBeforeEveryOverload_GoesToTheFirstOne()
    {
        var list = ReadInline($"""
            <class name="N.C" filename="C.cs">
              <methods>{Method("Run", """<line number="20" hits="1" branch="False"/>""")}</methods>
            </class>
            <class name="N.C/&lt;&gt;c" filename="C.cs">
              <methods>{Method("<Run>b__0", """<line number="18" hits="1" branch="False"/>""")}</methods>
            </class>
            """);

        var run = list.Single(c => c.IsMethodLevel);
        Assert.That((run.MethodLineNumber, run.LinesTotal), Is.EqualTo((18, 2)));
    }

    [Test]
    public void SeveralStateMachinesOfTheSameName_StayApart()
    {
        var list = ReadInline($"""
            <class name="N.C/&lt;Load&gt;d__0" filename="C.cs">
              <methods>{Method("MoveNext", """<line number="5" hits="1" branch="False"/>""")}</methods>
            </class>
            <class name="N.C/&lt;Load&gt;d__1" filename="C.cs">
              <methods>{Method("MoveNext", """<line number="25" hits="0" branch="False"/>""")}</methods>
            </class>
            """);

        var loads = list.Where(c => c.IsMethodLevel).OrderBy(c => c.MethodLineNumber).ToList();
        Assert.That(loads.Select(c => (c.MethodLineNumber, c.LinesCovered)), Is.EqualTo(new[] { (5, 1), (25, 0) }));
    }

    [Test]
    public void SeveralHoldersOfAMethodWithoutAnOrdinaryEntry_ShareOneEntry()
    {
        var list = ReadInline($"""
            <class name="N.C/&lt;&gt;c" filename="C.cs">
              <methods>{Method("<Run>b__0_0", """<line number="5" hits="1" branch="False"/>""")}</methods>
            </class>
            <class name="N.C/&lt;&gt;c__DisplayClass1_0" filename="C.cs">
              <methods>{Method("<Run>b__0", """<line number="6" hits="0" branch="False"/>""")}</methods>
            </class>
            """);

        var run = list.Single(c => c.IsMethodLevel);
        Assert.That((run.LinesCovered, run.LinesTotal, run.MethodLineNumber), Is.EqualTo((1, 2, 5)));
    }

    [Test]
    public void GeneratedClassWithoutAUsableMethodName_IsDropped()
    {
        var list = ReadInline($"""
            <class name="N.C/&lt;PrivateImplementationDetails&gt;" filename="C.cs">
              <methods>{Method(".ctor", """<line number="5" hits="1" branch="False"/>""")}</methods>
            </class>
            """);

        Assert.That(list, Is.Empty);
    }

    [Test]
    public void UserNestedClass_KeepsItsPartsApartFromGeneratedOnes_AndUsesDotsInItsName()
    {
        var list = ReadInline($"""
            <class name="N.Outer/Inner" filename="C.cs">
              <methods>{Method("Run", """<line number="5" hits="1" branch="False"/>""")}</methods>
            </class>
            <class name="N.Outer/Inner/&lt;Run&gt;d__0" filename="C.cs">
              <methods>{Method("MoveNext", """<line number="6" hits="1" branch="False"/>""")}</methods>
            </class>
            """);

        var run = list.Single(c => c.IsMethodLevel);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(run.FullyQualifiedClassName, Is.EqualTo("Asm.N.Outer.Inner"));
            Assert.That(run.LinesTotal, Is.EqualTo(2));
        }
    }

    [Test]
    public void ComplexityAndCountsOfMergedParts_AreSummed()
    {
        var list = ReadInline($"""
            <class name="N.C" filename="C.cs">
              <methods>{Method("Run", """<line number="5" hits="1" branch="True" condition-coverage="50% (1/2)"/>""", """complexity="2" """)}</methods>
            </class>
            <class name="N.C/&lt;&gt;c" filename="C.cs">
              <methods>{Method("<Run>b__0_0", """<line number="6" hits="1" branch="True" condition-coverage="100% (2/2)"/><line number="x" hits="1"/>""", """complexity="3" """)}</methods>
            </class>
            """);

        var run = list.Single(c => c.IsMethodLevel);
        Assert.That((run.Complexity, run.BranchesCovered, run.BranchesTotal), Is.EqualTo((5, 3, 4)));
    }

    // ---- matching ----

    private static CognitiveMetrics Metric(string name, int line, int lineCount) =>
        new(name, "N.C", "/src/C.cs", $"void {name}()", line, linesOfCode: lineCount);

    [Test]
    public void Matcher_MethodWithGeneratedData_NeverFallsBackToClassLevelCoverage()
    {
        var metric = Metric("Load", 5, 10);
        var coverage = new Coverage[]
        {
            new() { FilePath = "/src/C.cs", FullyQualifiedClassName = "N.C", LinesCovered = 9, LinesTotal = 10 },
            new() { FilePath = "/src/C.cs", FullyQualifiedClassName = "N.C", MethodName = "Load", MethodLineNumber = 99, LinesCovered = 1, LinesTotal = 2, FromGeneratedTypes = true },
        };

        var matches = CoverageMatcher.MatchCoverageToMetrics(new CognitiveMetricsCollection { metric }, coverage);

        Assert.That(matches, Is.Empty);
    }

    [Test]
    public void Matcher_MethodWithoutGeneratedData_StillFallsBackToClassLevelCoverage()
    {
        var metric = Metric("Load", 5, 10);
        var coverage = new Coverage[]
        {
            new() { FilePath = "/src/C.cs", FullyQualifiedClassName = "N.C", LinesCovered = 9, LinesTotal = 10 },
            new() { FilePath = "/src/C.cs", FullyQualifiedClassName = "N.C", MethodName = "Load", MethodLineNumber = 99, LinesCovered = 1, LinesTotal = 2 },
        };

        var matches = CoverageMatcher.MatchCoverageToMetrics(new CognitiveMetricsCollection { metric }, coverage);

        Assert.That(matches[metric].LinesCovered, Is.EqualTo(9));
    }

    [Test]
    public void Matcher_GeneratedEntryInsideTheMethod_Matches()
    {
        var metric = Metric("Load", 5, 10);
        var coverage = new Coverage { FilePath = "/src/C.cs", MethodName = "Load", MethodLineNumber = 7, LinesCovered = 1, LinesTotal = 2, FromGeneratedTypes = true };

        Assert.That(CoverageMatcher.MatchCoverageToMetrics(new CognitiveMetricsCollection { metric }, [coverage]), Has.Count.EqualTo(1));
    }
}
