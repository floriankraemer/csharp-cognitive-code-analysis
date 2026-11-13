/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.HalsteadAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class MetricsSorterTests
{
    // Three methods whose value is lowest for "low", then "mid", then "high" in every numeric field.
    private static CognitiveMetrics Method(string name, int level, string className = "C", string file = "a.cs", int line = 1)
    {
        var m = new CognitiveMetrics(
            name, className, file, name + "()", line,
            ifCount: level, elseCount: level, loopCount: level, switchCount: level, tryCatchCount: level,
            returnCount: level, argumentCount: level, linesOfCode: level, nestingLevels: level,
            cyclomaticComplexity: level, localVariableCount: level, fieldAccessCount: level, propertyAccessCount: level,
            halstead: new HalsteadMetrics { Volume = level })
        {
            totalScore = level,
            linesOfCodeScore = level, ifScore = level, elseScore = level, loopScore = level, switchScore = level,
            tryCatchScore = level, returnScore = level, argumentScore = level, nestingScore = level,
            localVariableScore = level, fieldAccessScore = level, propertyAccessScore = level,
        };
        return m;
    }

    private static CognitiveMetrics[] Shuffled() => [Method("mid", 2), Method("high", 3), Method("low", 1)];

    private static IEnumerable<string> NumericFields => MetricsSorter.FieldNames.Except(["class", "method", "file", "line"]);

    [TestCaseSource(nameof(NumericFields))]
    public void Sort_EveryNumericField_OrdersAscendingAndDescending(string field)
    {
        var asc = MetricsSorter.Create(field, "asc").Sort(Shuffled()).Select(m => m.MethodName);
        var desc = MetricsSorter.Create(field, "desc").Sort(Shuffled()).Select(m => m.MethodName);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(asc, Is.EqualTo(new[] { "low", "mid", "high" }), field);
            Assert.That(desc, Is.EqualTo(new[] { "high", "mid", "low" }), field);
        }
    }

    [Test]
    public void FieldNames_CoverEveryDocumentedField()
    {
        Assert.That(MetricsSorter.FieldNames, Is.EquivalentTo(new[]
        {
            "score", "class", "method", "file", "line", "halstead", "cyclomatic",
            "linesOfCode", "argumentCount", "returnCount", "localVariableCount", "propertyAccessCount", "fieldAccessCount",
            "ifCount", "elseCount", "nestingLevels", "loopCount", "switchCount", "tryCatchCount",
            "linesOfCodeScore", "argumentCountScore", "returnCountScore", "localVariableCountScore",
            "propertyAccessCountScore", "fieldAccessCountScore", "ifCountScore", "elseCountScore",
            "nestingLevelsScore", "loopCountScore", "switchCountScore", "tryCatchCountScore",
        }));
    }

    [TestCase("argumentScore", "argumentCountScore")]
    [TestCase("nestingScore", "nestingLevelsScore")]
    [TestCase("ifScore", "ifCountScore")]
    public void Sort_ReportColumnNameOfAScore_IsAcceptedButNotListed(string alias, string canonical)
    {
        Assert.That(MetricsSorter.FieldNames, Does.Not.Contain(alias));
        Assert.That(
            MetricsSorter.Create(alias, "desc").Sort(Shuffled()).Select(m => m.MethodName),
            Is.EqualTo(MetricsSorter.Create(canonical, "desc").Sort(Shuffled()).Select(m => m.MethodName)));
    }

    [Test]
    public void Sort_Strings_IgnoreLetterCase()
    {
        CognitiveMetrics[] methods = [Method("b", 1, className: "beta"), Method("a", 1, className: "Zeta"), Method("c", 1, className: "alpha")];

        var result = MetricsSorter.Create("class", "asc").Sort(methods).Select(m => m.ClassName);

        Assert.That(result, Is.EqualTo(new[] { "alpha", "beta", "Zeta" }));
    }

    [TestCase("method", new[] { "a", "B", "c" })]
    [TestCase("file", new[] { "c", "a", "B" })]
    public void Sort_MethodAndFile_OrderByText(string field, string[] expected)
    {
        CognitiveMetrics[] methods = [Method("B", 1, file: "z.cs"), Method("c", 1, file: "A.cs"), Method("a", 1, file: "b.cs")];

        var result = MetricsSorter.Create(field, "asc").Sort(methods).Select(m => m.MethodName);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void Sort_Line_OrdersNumericallyNotAsText()
    {
        CognitiveMetrics[] methods = [Method("m100", 1, line: 100), Method("m9", 1, line: 9), Method("m20", 1, line: 20)];

        var result = MetricsSorter.Create("line", "asc").Sort(methods).Select(m => m.MethodName);

        Assert.That(result, Is.EqualTo(new[] { "m9", "m20", "m100" }));
    }

    [Test]
    public void Sort_Halstead_TreatsMissingVolumeAsZero()
    {
        var withoutHalstead = new CognitiveMetrics("none", "C", "a.cs", "none()", 1);
        CognitiveMetrics[] methods = [Method("some", 1), withoutHalstead];

        var result = MetricsSorter.Create("halstead", "asc").Sort(methods).Select(m => m.MethodName);

        Assert.That(result, Is.EqualTo(new[] { "none", "some" }));
    }

    [TestCase("asc")]
    [TestCase("desc")]
    public void Sort_Ties_FallBackToAscendingClassMethodFileAndLineInBothDirections(string order)
    {
        CognitiveMetrics[] methods =
        [
            Method("m", 1, className: "B", file: "a.cs", line: 1),
            Method("z", 1, className: "A", file: "a.cs", line: 1),
            Method("a", 1, className: "A", file: "b.cs", line: 5),
            Method("a", 1, className: "A", file: "a.cs", line: 7),
            Method("a", 1, className: "A", file: "a.cs", line: 3),
        ];

        var result = MetricsSorter.Create("score", order).Sort(methods).Select(m => $"{m.ClassName}.{m.MethodName}@{m.FilePath}:{m.methodLineNumber}");

        Assert.That(result, Is.EqualTo(new[] { "A.a@a.cs:3", "A.a@a.cs:7", "A.a@b.cs:5", "A.z@a.cs:1", "B.m@a.cs:1" }));
    }

    [Test]
    public void Sort_FullyEqualMethods_KeepTheirOrder()
    {
        var first = Method("m", 1);
        var second = Method("m", 1);

        var result = MetricsSorter.Create("score", "desc").Sort([first, second]);

        Assert.That(result[0], Is.SameAs(first));
        Assert.That(result[1], Is.SameAs(second));
    }

    [TestCase("SCORE", "DESC")]
    [TestCase("Score", "Desc")]
    [TestCase(" score ", " desc ")]
    public void Create_AcceptsAnyLetterCase(string field, string order)
    {
        var result = MetricsSorter.Create(field, order).Sort(Shuffled()).Select(m => m.MethodName);

        Assert.That(result, Is.EqualTo(new[] { "high", "mid", "low" }));
    }

    [Test]
    public void Create_WithoutFieldOrOrder_KeepsTheGivenOrder()
    {
        CognitiveMetrics[] methods = Shuffled();

        Assert.That(MetricsSorter.Create(null, null).Sort(methods), Is.EqualTo(methods));
        Assert.That(MetricsSorter.Create(null, "desc").Sort(methods), Is.EqualTo(methods));
    }

    [Test]
    public void Create_UnknownField_ListsTheValidFields()
    {
        var ex = Assert.Throws<ArgumentException>(() => MetricsSorter.Create("bogus", "asc"))!;

        Assert.That(ex.Message, Does.StartWith("Invalid sort field \"bogus\". Valid fields: score, class, method,"));
        Assert.That(ex.Message, Does.Contain("tryCatchCountScore."));
    }

    [TestCase("up")]
    [TestCase("")]
    [TestCase("ascending")]
    public void Create_UnknownOrder_ListsTheValidValues(string order)
    {
        var ex = Assert.Throws<ArgumentException>(() => MetricsSorter.Create("score", order))!;

        Assert.That(ex.Message, Is.EqualTo($"Invalid sort order \"{order}\". Valid values: asc, desc."));
    }

    [Test]
    public void Create_UnknownOrder_IsRejectedEvenWithoutField()
    {
        Assert.Throws<ArgumentException>(() => MetricsSorter.Create(null, "up"));
    }

    [Test]
    public void Create_EmptyField_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => MetricsSorter.Create("", "asc"));
    }
}
