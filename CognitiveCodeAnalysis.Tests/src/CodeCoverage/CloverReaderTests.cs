/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

public class CloverReaderTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "fixtures", "clover", name);

    private List<Coverage> ReadInline(string xml) =>
        new CloverReader().ReadCoverage(_temp.CreateFileWithContent("clover.xml", xml)).ToList();

    [Test]
    public void PhpUnitStyle_ClassMetrics_AreStatementsAndConditionals()
    {
        var list = new CloverReader().ReadCoverage(Fixture("phpunit-style.clover.xml")).ToList();

        var cls = list.Single(c => !c.IsMethodLevel);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cls.FullyQualifiedClassName, Is.EqualTo("Demo.Math.Calculator"));
            Assert.That(cls.FilePath, Is.EqualTo(Path.GetFullPath("/proj/src/Calculator.cs")));
            Assert.That((cls.LinesCovered, cls.LinesTotal), Is.EqualTo((6, 8)));
            Assert.That(cls.LineCoveragePercentage, Is.EqualTo(75.0));
            Assert.That((cls.BranchesCovered, cls.BranchesTotal), Is.EqualTo((3, 4)));
            Assert.That(cls.BranchCoveragePercentage, Is.EqualTo(75.0));
            Assert.That(cls.Complexity, Is.EqualTo(5));
        }
    }

    [Test]
    public void PhpUnitStyle_Methods_OwnTheStatementsUpToTheNextMethod()
    {
        var methods = new CloverReader().ReadCoverage(Fixture("phpunit-style.clover.xml"))
            .Where(c => c.IsMethodLevel).ToDictionary(c => c.MethodName);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(methods.Keys, Is.EqualTo(new[] { "Add", "Divide", "Unused" }));
            Assert.That(methods["Add"].MethodLineNumber, Is.EqualTo(7));
            Assert.That((methods["Add"].LinesCovered, methods["Add"].LinesTotal), Is.EqualTo((1, 1)));
            Assert.That(methods["Add"].BranchCoveragePercentage, Is.Null, "a method without conditionals has no branch data");
            Assert.That((methods["Divide"].LinesCovered, methods["Divide"].LinesTotal), Is.EqualTo((2, 3)));
            Assert.That((methods["Divide"].BranchesCovered, methods["Divide"].BranchesTotal), Is.EqualTo((1, 2)));
            Assert.That(methods["Divide"].Complexity, Is.EqualTo(3));
            Assert.That(methods["Unused"].LineCoveragePercentage, Is.EqualTo(0.0));
            Assert.That(methods["Add"].FullyQualifiedClassName, Is.EqualTo("Demo.Math.Calculator"));
        }
    }

    [Test]
    public void OpenCloverStyle_ClassesGetTheirPackage_AndTestProjectIsIgnored()
    {
        var classes = new CloverReader().ReadCoverage(Fixture("openclover-style.clover.xml"))
            .Where(c => !c.IsMethodLevel).ToList();

        Assert.That(classes.Select(c => c.FullyQualifiedClassName),
            Is.EqualTo(new[] { "Shapes.Shapes", "Shapes.Inner", "Loose" }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(classes[0].FilePath, Is.EqualTo(Path.GetFullPath("/proj/src/Shapes/Shapes.cs")), "path wins over the simple name");
            Assert.That(classes[0].LineCoveragePercentage, Is.EqualTo(75.0));
            Assert.That(classes[1].BranchCoveragePercentage, Is.Null, "no conditionals means no branch data");
        }
    }

    [Test]
    public void OpenCloverStyle_MethodsComeFromTheSignatureWithoutParameters()
    {
        var methods = new CloverReader().ReadCoverage(Fixture("openclover-style.clover.xml"))
            .Where(c => c.IsMethodLevel).ToList();

        Assert.That(methods.Select(m => m.MethodName), Is.EqualTo(new[] { "Shapes", "Area", "Perimeter", "Never" }));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(methods[1].MethodLineNumber, Is.EqualTo(11));
            Assert.That((methods[1].LinesCovered, methods[1].LinesTotal), Is.EqualTo((1, 2)));
            Assert.That((methods[1].BranchesCovered, methods[1].BranchesTotal), Is.EqualTo((2, 2)));
            Assert.That(methods[2].LineCoveragePercentage, Is.EqualTo(100.0), "a method without statements is as covered as it is called");
            Assert.That(methods[3].LineCoveragePercentage, Is.EqualTo(0.0));
        }
    }

    [Test]
    public void ClassWithoutMetrics_HasNoFigures()
    {
        var list = ReadInline("<coverage><project><file name=\"/p/A.cs\"><class name=\"A\"/></file></project></coverage>");

        var cls = list.Single();
        Assert.That((cls.LinesTotal, cls.BranchesTotal), Is.EqualTo((0, 0)));
        Assert.That(cls.BranchCoveragePercentage, Is.Null);
    }

    [Test]
    public void FileWithoutNameAndPath_HasNoFilePath()
    {
        var list = ReadInline("<coverage><project><file><class name=\"A\"/></file></project></coverage>");

        Assert.That(list.Single().FilePath, Is.Empty);
    }

    [Test]
    public void ConditionalLine_CountsEachCoveredSideOnce()
    {
        var list = ReadInline("""
            <coverage><project><file name="/p/A.cs">
              <line num="1" type="method" name="M" count="1"/>
              <line num="2" type="cond" truecount="0" falsecount="5"/>
              <line num="3" type="cond" truecount="0" falsecount="0"/>
            </file></project></coverage>
            """);

        Assert.That((list.Single().BranchesCovered, list.Single().BranchesTotal), Is.EqualTo((1, 4)));
    }

    [Test]
    public void MethodsOfAFileWithSeveralClasses_BelongToTheFirstClass()
    {
        var list = ReadInline("""
            <coverage><project><file name="/p/A.cs">
              <class name="First" namespace="N"/><class name="Second" namespace="N"/>
              <line num="1" type="method" name="M" count="1"/>
            </file></project></coverage>
            """);

        Assert.That(list.Single(c => c.IsMethodLevel).FullyQualifiedClassName, Is.EqualTo("N.First"));
    }

    [Test]
    public void ProjectWithoutFiles_YieldsNothing()
    {
        Assert.That(ReadInline("<coverage><project/></coverage>"), Is.Empty);
    }

    [Test]
    public void NonNumericAttributes_CountAsZero()
    {
        var list = ReadInline("<coverage><project><file name=\"/p/A.cs\"><class name=\"A\"><metrics statements=\"x\" coveredstatements=\"\"/></class></file></project></coverage>");

        Assert.That(list.Single().LinesTotal, Is.EqualTo(0));
    }

    [TestCase("<coverage/>")]
    [TestCase("<report><project/></report>")]
    public void NotAClover_IsRejectedWithFileAndReason(string xml)
    {
        var path = _temp.CreateFileWithContent("other.xml", xml);

        var ex = Assert.Throws<InvalidCoverageFileException>(() => new CloverReader().ReadCoverage(path));

        Assert.That(ex!.Message, Does.StartWith($"Invalid coverage file {path}: not a Clover report"));
    }

    [Test]
    public void CoberturaFile_IsNotAClover()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", "coverlet", "churn-calculator.coverlet.cobertura.xml"));

        Assert.Throws<InvalidCoverageFileException>(() => new CloverReader().ReadCoverage(path));
    }

    [Test]
    public void MissingFile_ThrowsFileNotFound()
    {
        Assert.Throws<FileNotFoundException>(() => new CloverReader().ReadCoverage(Path.Combine(_temp.tmpDirectory, "none.xml")));
    }

    [Test]
    public void MalformedXml_IsRejectedWithFileAndReason()
    {
        var path = _temp.CreateFileWithContent("bad.xml", "<coverage><project>");

        var ex = Assert.Throws<InvalidCoverageFileException>(() => new CloverReader().ReadCoverage(path));

        Assert.That(ex!.Message, Does.StartWith($"Invalid coverage file {path}: "));
    }
}
