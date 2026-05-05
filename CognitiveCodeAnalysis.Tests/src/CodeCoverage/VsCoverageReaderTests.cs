/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

public class VsCoverageReaderTests
{
    private TempFiles _tempFiles = null!;

    [SetUp]
    public void SetUp()
    {
        _tempFiles = new TempFiles();
    }

    [TearDown]
    public void TearDown()
    {
        _tempFiles.CleanUp();
    }

    [Test]
    public void ReadCoverage_ParsesRangesAndEmitsFileAggregate()
    {
        string sourcePath = _tempFiles.CreateFile("Tracked.cs");
        string xmlPath = CreateVsCoverageXmlForSource(sourcePath);

        var reader = new VsCoverageReader();
        var list = reader.ReadCoverage(xmlPath).ToList();

        Assert.That(list, Has.Count.EqualTo(1));
        Coverage c = list[0];
        Assert.That(c.FullyQualifiedClassName, Is.Empty);
        Assert.That(c.MethodName, Is.Empty);
        Assert.That(c.MethodLineNumber, Is.EqualTo(0));
        Assert.That(c.LinesTotal, Is.EqualTo(3));
        Assert.That(c.LinesCovered, Is.EqualTo(2));
        Assert.That(Path.GetFullPath(c.FilePath), Is.EqualTo(Path.GetFullPath(sourcePath)));
        Assert.That(c.BranchesTotal, Is.EqualTo(0));
        Assert.That(c.Complexity, Is.EqualTo(0));
    }

    [Test]
    public void ReadCoverage_FileNotFound_Throws()
    {
        var reader = new VsCoverageReader();
        Assert.Throws<FileNotFoundException>(() => reader.ReadCoverage(Path.Combine(_tempFiles.tmpDirectory, "nope.xml")).ToList());
    }

    [Test]
    public void ReadCoverage_InvalidRoot_Throws()
    {
        string path = _tempFiles.CreateFileWithContent("bad.xml", """<?xml version="1.0"?><notresults/>""");
        var reader = new VsCoverageReader();
        var ex = Assert.Throws<InvalidOperationException>(() => reader.ReadCoverage(path).ToList());
        Assert.That(ex!.Message, Does.Contain("results"));
    }

    [Test]
    public void AutoDetectCoverageReader_SelectsVsReader_ForResultsRoot()
    {
        string sourcePath = _tempFiles.CreateFile("X.cs");
        string xmlPath = CreateVsCoverageXmlForSource(sourcePath);

        var reader = new AutoDetectCoverageReader();
        var list = reader.ReadCoverage(xmlPath).ToList();

        Assert.That(list, Has.Count.EqualTo(1));
        Assert.That(list[0].LinesTotal, Is.EqualTo(3));
    }

    private string CreateVsCoverageXmlForSource(string sourcePath)
    {
        string fixtureDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures"));
        string template = File.ReadAllText(Path.Combine(fixtureDir, "vs-coverage-sample.xml"));
        string xml = template.Replace("__SOURCE_PATH__", sourcePath, StringComparison.Ordinal);
        return _tempFiles.CreateFileWithContent("coverage.xml", xml);
    }
}
