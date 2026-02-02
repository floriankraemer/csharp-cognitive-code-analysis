using CognitiveCodeAnalysis.CodeCoverage;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

public class CoberturaReaderTests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void ReadCoberturaFile()
    {
        //var reader = new CoberturaReader();

        //reader.ReadCoverage("TestData/cobertura-coverage.xml");
    }

    [Test]
    public void ReadNoneExistentCoberturaFile()
    {
        //var reader = new CoberturaReader();

        //reader.ReadCoverage("file-does-not-exist.xml");
    }
}
