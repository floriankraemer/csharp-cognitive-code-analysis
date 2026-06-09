/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class CognitiveAnalysisFacadeTests
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
            new ScoreCalculator(),
            new CoberturaReader(),
            new ClassCouplingAnalyser()
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
    public void TestFindFiles_ArrayOverload()
    {
        _tempFiles.CreateFile("A.cs");
        _tempFiles.CreateFile("B.cs");

        // Call the overload that accepts string[]
        var files = _facade.FindSourceFiles([_tempFiles.tmpDirectory]);

        Assert.That(files, Has.Count.EqualTo(2));
        Assert.That(files, Has.Some.EndsWith("A.cs"));
        Assert.That(files, Has.Some.EndsWith("B.cs"));
    }

    [Test]
    public void TestAnalysis()
    {
        var files = _facade.FindSourceFiles("../../fixtures");
        var metricsCollection = _facade.AnalyseSourceFiles(files);

        Assert.That(metricsCollection , Has.Count.EqualTo(0));
    }

    [Test]
    public void TestAnalyseSourceFiles_OnTemporaryFile_ProducesMetrics()
    {
        var content = @"
namespace X {
    public class Y {
        public void DoWork() {
            if (true) { }
        }
    }
}";
        var file = _tempFiles.CreateFileWithContent("Simple.cs", content);

        var files = _facade.FindSourceFiles(_tempFiles.tmpDirectory);

        // Act
        var metricsCollection = _facade.AnalyseSourceFiles(files);

        // Assert - ensure we found one method and it's named correctly
        Assert.That(metricsCollection.Count, Is.EqualTo(1));
        var metrics = metricsCollection.First();
        Assert.That(metrics.MethodName, Is.EqualTo("DoWork"));
        Assert.That(metrics.FilePath, Is.Not.Null.And.Not.Empty);
        Assert.That(metrics.linesOfCode, Is.GreaterThan(0));
        Assert.That(metrics.cyclomaticComplexity, Is.GreaterThanOrEqualTo(2));
        Assert.That(metrics.Halstead, Is.Not.Null);
    }

    [Test]
    public void LoadCoverageData_WithMatchingCoverage_UpdatesMetricsAndReturnsSuccess()
    {
        // Arrange
        string filePath = Path.Combine(_tempFiles.tmpDirectory, "C.cs");
        _tempFiles.CreateFileWithContent("C.cs", "namespace A { class B { void M() { } } }");

        var metrics = new CognitiveMetrics(
            methodName: "M",
            className: "A.B",
            filePath: Path.GetFullPath(filePath),
            methodSignature: "void M()",
            methodLineNumber: 1
        );

        var metricsCollection = new CognitiveMetricsCollection {
            metrics
        };

        // Coverage that matches the metrics (method level)
        var coverage = new Coverage
        {
            FilePath = Path.GetFullPath(filePath),
            MethodName = "M",
            MethodLineNumber = metrics.methodLineNumber,
            LinesCovered = 1,
            LinesTotal = 2,
            BranchesCovered = 0,
            BranchesTotal = 0
        };

        var facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(),
            new CognitiveCodeAnalyser(),
            new CognitiveConfiguration(),
            new ScoreCalculator(),
            new FakeCoverageReaderReturn( new[] { coverage } ),
            new ClassCouplingAnalyser()
        );

        // Act
        var result = facade.LoadCoverageData("ignored-for-fake", metricsCollection);

        // Assert
        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Success , Is.True);
            Assert.That(metrics.lineCoveragePercentage, Is.Not.Null);
            Assert.That(metrics.lineCoveragePercentage.HasValue , Is.True);
            Assert.That(metrics.lineCoveragePercentage!.Value , Is.EqualTo(coverage.LineCoveragePercentage));
            Assert.That(metrics.HasCoverageData , Is.True);

            // churnScore will be set (possibly 0 if totalScore == 0)
            Assert.That(metrics.churnScore.HasValue , Is.True);
        }
    }

    [Test]
    public void LoadCoverageData_WithNoCoverageEntries_ReturnsNoDataResult()
    {
        // Arrange
        var metricsCollection = new CognitiveMetricsCollection();

        var facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(),
            new CognitiveCodeAnalyser(),
            new CognitiveConfiguration(),
            new ScoreCalculator(),
            new FakeCoverageReaderReturn(Enumerable.Empty<Coverage>()),
            new ClassCouplingAnalyser()
        );

        // Act
        var result = facade.LoadCoverageData("some-file", metricsCollection);

        // Assert
        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Success , Is.False);
            Assert.That(result.ErrorMessage , Is.EqualTo("No coverage data found"));
        }
    }

    [Test]
    public void LoadCoverageData_WithCoverageButNoMatches_ReturnsSuccess()
    {
        // Arrange
        // Coverage for a different file/class so no matches expected
        var coverage = new Coverage
        {
            FilePath = Path.GetFullPath(Path.Combine(_tempFiles.tmpDirectory, "Other.cs")),
            MethodName = "Other",
            MethodLineNumber = 1,
            LinesCovered = 1,
            LinesTotal = 1
        };

        var metricsCollection = new CognitiveMetricsCollection(); // empty metrics -> no matches

        var facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(),
            new CognitiveCodeAnalyser(),
            new CognitiveConfiguration(),
            new ScoreCalculator(),
            new FakeCoverageReaderReturn( new[] { coverage } ),
            new ClassCouplingAnalyser()
        );

        // Act
        var result = facade.LoadCoverageData("some-file", metricsCollection);

        // Assert - per implementation, non-empty coverage with no matches returns Success = true
        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Success , Is.True);
            Assert.That(result.ErrorMessage , Is.Null);
        }
    }

    [Test]
    public void LoadCoverageData_FileNotFoundException_ReturnsErrorMessage()
    {
        // Arrange
        var metricsCollection = new CognitiveMetricsCollection();

        var facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(),
            new CognitiveCodeAnalyser(),
            new CognitiveConfiguration(),
            new ScoreCalculator(),
            new FakeCoverageReaderThrowFileNotFound(),
            new ClassCouplingAnalyser()
        );

        // Act
        var result = facade.LoadCoverageData("missing.xml", metricsCollection);

        // Assert
        using (Assert.EnterMultipleScope()) {
            Assert.That(result.Success , Is.False);
            Assert.That(result.ErrorMessage , Does.Contain("Coverage file not found"));
        }
    }

    // ---- Helpers / Fakes ----

    private class FakeCoverageReaderReturn : ICoverageReader
    {
        private readonly IEnumerable<Coverage> _return;

        public FakeCoverageReaderReturn(IEnumerable<Coverage> items)
        {
            _return = items;
        }

        public IEnumerable<Coverage> ReadCoverage(string filePath)
        {
            // ignore filePath - return configured enumerable
            return _return;
        }
    }

    private class FakeCoverageReaderThrowFileNotFound : ICoverageReader
    {
        public IEnumerable<Coverage> ReadCoverage(string filePath)
        {
            throw new FileNotFoundException("not found", filePath);
        }
    }
}
