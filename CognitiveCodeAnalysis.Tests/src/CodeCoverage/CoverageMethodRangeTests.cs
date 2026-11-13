/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

/// <summary>
/// Coverage writers report the first executable line of a method, so a method matches by name and by a first
/// coverage line inside the half-open range [declaration line, declaration line + line count).
/// </summary>
public class CoverageMethodRangeTests
{
    private static readonly string File_ = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "cov-range", "a.cs"));

    private static CognitiveMetrics Method(string name, int declarationLine, int lineCount, string? file = null) =>
        new(name, "N.C", file ?? File_, $"void {name}()", declarationLine, linesOfCode: lineCount);

    private static Coverage MethodCov(string name, int firstLine, int linesCovered = 1, string? file = null) =>
        new() { FilePath = file ?? File_, MethodName = name, MethodLineNumber = firstLine, FullyQualifiedClassName = "N.C", LinesCovered = linesCovered, LinesTotal = 2 };

    private static Dictionary<CognitiveMetrics, Coverage> Match(CognitiveMetrics m, params Coverage[] coverage)
        => CoverageMatcher.MatchCoverageToMetrics(new CognitiveMetricsCollection { m }, coverage);

    [TestCase(10, true, TestName = "declaration line")]
    [TestCase(12, true, TestName = "first body line")]
    [TestCase(14, true, TestName = "last line of the method")]
    [TestCase(9, false, TestName = "line before the declaration")]
    [TestCase(15, false, TestName = "line after the method")]
    [TestCase(0, false, TestName = "no coverage line at all")]
    public void FirstCoverageLine_MustLieWithinTheMethodsLines(int firstLine, bool matches)
    {
        var m = Method("Run", declarationLine: 10, lineCount: 5);

        Assert.That(Match(m, MethodCov("Run", firstLine)), matches ? Has.Count.EqualTo(1) : Is.Empty);
    }

    [Test]
    public void Overloads_AreToldApartByTheirLines()
    {
        var first = Method("Run", 10, 5);
        var second = Method("Run", 20, 5);
        var collection = new CognitiveMetricsCollection { first, second };

        var matches = CoverageMatcher.MatchCoverageToMetrics(collection, [MethodCov("Run", 22, linesCovered: 2), MethodCov("Run", 12, linesCovered: 1)]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(matches[first].LinesCovered, Is.EqualTo(1));
            Assert.That(matches[second].LinesCovered, Is.EqualTo(2));
        }
    }

    [Test]
    public void SameNameAndRange_InAnotherFile_DoesNotMatch()
    {
        var m = Method("Run", 10, 5);

        Assert.That(Match(m, MethodCov("Run", 12, file: Path.Combine(Path.GetDirectoryName(File_)!, "b.cs"))), Is.Empty);
    }

    [Test]
    public void DifferentName_DoesNotMatchEvenInsideTheRange()
    {
        var m = Method("Run", 10, 5);

        Assert.That(Match(m, MethodCov("Walk", 12)), Is.Empty);
    }

    [Test]
    public void MethodWithoutLineCount_StillMatchesItsDeclarationLine()
    {
        var m = Method("Run", 10, 0);

        Assert.That(Match(m, MethodCov("Run", 10)), Has.Count.EqualTo(1));
        Assert.That(Match(m, MethodCov("Run", 11)), Is.Empty);
    }

    [Test]
    public void MethodMatch_WinsOverClassLevelEntry()
    {
        var m = Method("Run", 10, 5);
        var classLevel = new Coverage { FilePath = File_, FullyQualifiedClassName = "N.C", LinesCovered = 9, LinesTotal = 10 };

        var matches = CoverageMatcher.MatchCoverageToMetrics(new CognitiveMetricsCollection { m }, [classLevel, MethodCov("Run", 12, linesCovered: 1)]);

        Assert.That(matches[m].LinesCovered, Is.EqualTo(1));
    }

    [Test]
    public void OutsideEntry_FallsBackToTheClassLevelEntry()
    {
        var m = Method("Run", 10, 5);
        var classLevel = new Coverage { FilePath = File_, FullyQualifiedClassName = "N.C", LinesCovered = 9, LinesTotal = 10 };

        var matches = CoverageMatcher.MatchCoverageToMetrics(new CognitiveMetricsCollection { m }, [classLevel, MethodCov("Run", 99)]);

        Assert.That(matches[m].LinesCovered, Is.EqualTo(9));
    }
}
