using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

class CognitiveAnalysisFacadeTests
{
    private CognitiveAnalysisFacade _facade;
    private TempFiles _tempFiles;

    [SetUp]
    public void SetUp()
    {
        _tempFiles = new TempFiles();

        _facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(),
            new CognitiveCodeAnalyser(),
            new CognitiveConfiguration(),
            new ScoreCalculator(
                new CognitiveConfiguration()
            ),
            new CoberturaReader()
        );
    }

    [TearDown]
    public void TearDown()
    {
        _tempFiles.CleanUp();
    }

    [Test]
    public void TestFindFiles()
    {
        _tempFiles.CreateFile("Test.cs");
        _tempFiles.CreateFile("Test2.cs");

        var files = _facade.FindSourceFiles(_tempFiles.tmpDirectory);

        Assert.That(files , Has.Count.EqualTo(2));
    }

    [Test]
    public void TestAnalysis()
    {
        var files = _facade.FindSourceFiles("../../fixtures");
        var metricsCollection = _facade.AnalyseSourceFiles(files);

        Assert.That(0, Is.EqualTo(metricsCollection.Count));
    }
}
