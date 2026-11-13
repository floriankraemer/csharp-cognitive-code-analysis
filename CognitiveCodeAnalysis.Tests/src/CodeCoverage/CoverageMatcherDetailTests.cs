/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

public class CoverageMatcherDetailTests
{
    private static readonly string Dir = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cov-matcher"));
    private static string P(string name) => Path.Combine(Dir, name);

    private static CognitiveMetrics Method(string cls, string name, int line, string file) =>
        new(name, cls, file, $"void {name}()", line);

    private static Coverage MethodCov(string file, string name, int line, string fqcn = "N.C", int covered = 1) =>
        new() { FilePath = file, MethodName = name, MethodLineNumber = line, FullyQualifiedClassName = fqcn, LinesCovered = covered, LinesTotal = 2 };

    private static Coverage ClassCov(string file, string fqcn, int covered = 1) =>
        new() { FilePath = file, FullyQualifiedClassName = fqcn, LinesCovered = covered, LinesTotal = 2 };

    private static Coverage FileCov(string file, int covered = 1) =>
        new() { FilePath = file, LinesCovered = covered, LinesTotal = 2 };

    private static Dictionary<CognitiveMetrics, Coverage> Match(CognitiveMetrics m, params Coverage[] cov)
        => CoverageMatcher.MatchCoverageToMetrics(new CognitiveMetricsCollection { m }, cov);

    [Test]
    public void Method_RequiresSameNameAndLine()
    {
        var m = Method("C", "Run", 10, P("a.cs"));

        Assert.That(Match(m, MethodCov(P("a.cs"), "Run", 10)), Has.Count.EqualTo(1));
        Assert.That(Match(m, MethodCov(P("a.cs"), "Run", 11)), Is.Empty);
        Assert.That(Match(m, MethodCov(P("a.cs"), "Other", 10)), Is.Empty);
        Assert.That(Match(m, MethodCov(P("b.cs"), "Run", 10)), Is.Empty);
    }

    [Test]
    public void Paths_AreMatchedIgnoringCaseSeparatorsAndTrailingSlash()
    {
        var m = Method("C", "Run", 10, P("Dir") + "/A.cs");

        var matches = Match(m, MethodCov(P("dir") + "\\a.cs", "Run", 10));

        Assert.That(matches, Has.Count.EqualTo(1));
    }

    [Test]
    public void ClassLevel_MatchesExactFqcn()
    {
        var m = Method("N.C", "Run", 10, P("a.cs"));

        Assert.That(Match(m, ClassCov(P("a.cs"), "N.C")), Has.Count.EqualTo(1));
        Assert.That(Match(m, ClassCov(P("a.cs"), "N.D")), Is.Empty);
    }

    [Test]
    public void ClassLevel_MatchesWhenCoverageFqcnEndsWithMetricsClassName()
    {
        var m = Method("Engine", "Run", 10, P("a.cs"));

        Assert.That(Match(m, ClassCov(P("a.cs"), "MyNs.Engine")), Has.Count.EqualTo(1));
        Assert.That(Match(m, ClassCov(P("a.cs"), "MyNs.NotEngine")), Is.Empty);
        Assert.That(Match(m, ClassCov(P("a.cs"), "MyNsEngine")), Is.Empty);
    }

    [Test]
    public void ClassLevel_MatchesWhenMetricsClassNameEndsWithCoverageFqcn()
    {
        var m = Method("MyNs.Engine", "Run", 10, P("a.cs"));

        Assert.That(Match(m, ClassCov(P("a.cs"), "Engine")), Has.Count.EqualTo(1));
        Assert.That(Match(m, ClassCov(P("a.cs"), "gine")), Is.Empty);
    }

    [Test]
    public void ClassLevel_IgnoresClassesOfOtherFiles()
    {
        var m = Method("Engine", "Run", 10, P("a.cs"));

        Assert.That(Match(m, ClassCov(P("b.cs"), "MyNs.Engine")), Is.Empty);
    }

    [Test]
    public void Precedence_MethodThenClassThenFile()
    {
        var m = Method("N.C", "Run", 10, P("a.cs"));
        var method = MethodCov(P("a.cs"), "Run", 10, covered: 2);
        var cls = ClassCov(P("a.cs"), "N.C", covered: 3);
        var file = FileCov(P("a.cs"), covered: 4);

        Assert.That(Match(m, file, cls, method)[m], Is.SameAs(method));
        Assert.That(Match(m, file, cls)[m], Is.SameAs(cls));
        Assert.That(Match(m, file)[m], Is.SameAs(file));
    }

    [Test]
    public void DuplicateEntries_FirstOneWins()
    {
        var m = Method("N.C", "Run", 10, P("a.cs"));
        var first = MethodCov(P("a.cs"), "Run", 10, covered: 1);
        var second = MethodCov(P("A.CS"), "Run", 10, covered: 2);

        Assert.That(Match(m, first, second)[m], Is.SameAs(first));
    }

    [Test]
    public void EmptyPaths_MatchEachOtherAsEmptyKey()
    {
        var m = Method("C", "Run", 1, string.Empty);

        Assert.That(Match(m, FileCov(string.Empty)), Has.Count.EqualTo(1));
    }

    [Test]
    public void NormalizePath_Variants()
    {
        Assert.That(CoverageMatcher.NormalizePath(""), Is.Empty);
        Assert.That(CoverageMatcher.NormalizePath("/a/b/"), Is.EqualTo("/a/b"));
        Assert.That(CoverageMatcher.NormalizePath("/a\\b"), Is.EqualTo("/a/b"));
        Assert.That(CoverageMatcher.NormalizePath("rel.cs"), Is.EqualTo(Path.GetFullPath("rel.cs").Replace('\\', '/')));
    }

    [Test]
    public void NormalizePath_InvalidPath_FallsBackToSeparatorNormalisedInput()
    {
        Assert.That(CoverageMatcher.NormalizePath("bad\0name\\x/"), Is.EqualTo("bad\0name/x"));
    }

    [Test]
    public void Progress_ReportsStartBatchesAndCompletion()
    {
        var collection = new CognitiveMetricsCollection();
        for (int i = 0; i < 250; i++)
        {
            collection.Add(Method("C", "M" + i, i, P("a.cs")));
        }

        var progress = new AnalysisProgressCollector();
        CoverageMatcher.MatchCoverageToMetrics(collection, [], progress);

        var reports = progress.Reports;
        Assert.That(reports.Select(r => (r.Phase, r.ProcessedFiles)), Is.EqualTo(new[]
        {
            (AnalysisProgressPhase.ApplyingCoverage, 0),
            (AnalysisProgressPhase.ApplyingCoverage, 100),
            (AnalysisProgressPhase.ApplyingCoverage, 200),
            (AnalysisProgressPhase.ApplyingCoverage, 250),
            (AnalysisProgressPhase.CoverageApplied, 250),
        }));
        Assert.That(reports.All(r => r.TotalFiles == 250), Is.True);
    }

    [Test]
    public void EmptyInputs_ProduceNoMatches()
    {
        Assert.That(CoverageMatcher.MatchCoverageToMetrics(new CognitiveMetricsCollection(), []), Is.Empty);
    }
}

public class CoverageMatcherConstructorTests
{
    private static readonly string SourceFile = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cov-ctor", "a.cs"));

    private static CognitiveMetrics Unit(string cls, string name, string signature) =>
        new(name, cls, SourceFile, signature, 10, linesOfCode: 3);

    private static Coverage MethodCov(string name, int line, int covered) =>
        new() { FilePath = SourceFile, MethodName = name, MethodLineNumber = line, FullyQualifiedClassName = "N.C", LinesCovered = covered, LinesTotal = 4 };

    private static Coverage? Match(CognitiveMetrics m, params Coverage[] cov) =>
        CoverageMatcher.MatchCoverageToMetrics(new CognitiveMetricsCollection { m }, cov).GetValueOrDefault(m);

    [Test]
    public void Constructor_MatchesCtor()
    {
        Assert.That(Match(Unit("N.C", "C", "public C(int a)"), MethodCov(".cctor", 11, 1), MethodCov(".ctor", 11, 2))?.LinesCovered, Is.EqualTo(2));
    }

    [Test]
    public void StaticConstructor_MatchesCctor()
    {
        Assert.That(Match(Unit("N.C", "C", "static C()"), MethodCov(".ctor", 11, 2), MethodCov(".cctor", 11, 1))?.LinesCovered, Is.EqualTo(1));
    }

    [Test]
    public void ConstructorOfAGlobalType_MatchesCtor()
    {
        Assert.That(Match(Unit("C", "C", "C()"), MethodCov(".ctor", 11, 2))?.LinesCovered, Is.EqualTo(2));
    }

    [Test]
    public void MethodNamedLikeAnotherType_IsNotAConstructor()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(Match(Unit("N.C", "D", "public void D()"), MethodCov(".ctor", 11, 2)), Is.Null);
            Assert.That(Match(Unit("N.C", "D", "public void D()"), MethodCov("D", 11, 2))?.LinesCovered, Is.EqualTo(2));
        }
    }

    [Test]
    public void StaticModifierAsPartOfAName_DoesNotMakeAStaticConstructor()
    {
        Assert.That(Match(Unit("N.C", "C", "public C(staticValue x)"), MethodCov(".ctor", 11, 2))?.LinesCovered, Is.EqualTo(2));
    }
}
