using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.NUnitTests.CognitiveAnalysis;

class CognitiveAnalysisFacadeTests
{
    private CognitiveAnalysisFacade _facade;

    [SetUp]
    public void SetUp()
    {
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

    [Test]
    public void TestFacade()
    {
    }
}
