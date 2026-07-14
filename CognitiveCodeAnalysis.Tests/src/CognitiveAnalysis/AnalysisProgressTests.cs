/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class AnalysisProgressTests
{
    private TempFiles _tempFiles = null!;
    private CognitiveConfiguration _configuration = null!;

    [SetUp]
    public void SetUp()
    {
        _tempFiles = new TempFiles();
        _configuration = new CognitiveConfiguration();
    }

    [TearDown]
    public void TearDown()
    {
        _tempFiles.CleanUp();
    }

    [Test]
    public void FindSourceFiles_ReportsSearchPhases()
    {
        _tempFiles.CreateFileWithContent("File1.cs", "// test");
        _tempFiles.CreateFileWithContent("File2.cs", "// test");

        var collector = new AnalysisProgressCollector();
        var finder = new SourceFileFinder();

        finder.FindSourceFiles([_tempFiles.tmpDirectory], collector);

        var reports = collector.Reports;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reports, Has.Count.EqualTo(2));
            Assert.That(reports[0].Phase, Is.EqualTo(AnalysisProgressPhase.SearchingFiles));
            Assert.That(reports[1].Phase, Is.EqualTo(AnalysisProgressPhase.SearchCompleted));
            Assert.That(reports[1].TotalFiles, Is.EqualTo(2));
        }
    }

    [Test]
    public void FindSourceFiles_WithNoProgress_DoesNotThrow()
    {
        var file1 = _tempFiles.CreateFileWithContent("File1.cs", "// test");
        var file2 = _tempFiles.CreateFileWithContent("File2.cs", "// test");
        var finder = new SourceFileFinder();

        var result = finder.FindSourceFiles([_tempFiles.tmpDirectory]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Has.Count.EqualTo(2));
            Assert.That(result, Contains.Item(file1));
            Assert.That(result, Contains.Item(file2));
        }
    }

    [Test]
    public void FindSourceFiles_EmptyDirectory_ReportsZeroCount()
    {
        var collector = new AnalysisProgressCollector();
        var finder = new SourceFileFinder();

        finder.FindSourceFiles([_tempFiles.tmpDirectory], collector);

        var reports = collector.Reports;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reports, Has.Count.EqualTo(2));
            Assert.That(reports[1].Phase, Is.EqualTo(AnalysisProgressPhase.SearchCompleted));
            Assert.That(reports[1].TotalFiles, Is.EqualTo(0));
        }
    }

    [Test]
    public async Task AnalyseFilesAsync_ReportsPerFileProgress()
    {
        const string content = @"
namespace X {
    public class Y {
        public void Run() { }
    }
}";
        _tempFiles.CreateFileWithContent("File1.cs", content);
        _tempFiles.CreateFileWithContent("File2.cs", content);
        _tempFiles.CreateFileWithContent("File3.cs", content);

        var collector = new AnalysisProgressCollector();
        var analyser = new CognitiveCodeAnalyser();
        var files = Directory.GetFiles(_tempFiles.tmpDirectory, "*.cs").ToList();

        await analyser.AnalyseFilesAsync(files, _configuration, collector);

        var reports = collector.Reports;
        var analysingReports = reports.Where(r => r.Phase == AnalysisProgressPhase.AnalysingFiles).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(analysingReports, Has.Count.EqualTo(4));
            Assert.That(analysingReports[0].ProcessedFiles, Is.EqualTo(0));
            Assert.That(analysingReports[0].TotalFiles, Is.EqualTo(3));
            Assert.That(analysingReports.Skip(1).Select(r => r.ProcessedFiles), Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(reports[^1].Phase, Is.EqualTo(AnalysisProgressPhase.AnalysisCompleted));
            Assert.That(reports[^1].ProcessedFiles, Is.EqualTo(3));
            Assert.That(reports[^1].TotalFiles, Is.EqualTo(3));
        }
    }

    [Test]
    public async Task AnalyseFilesAsync_WithNoProgress_DoesNotThrow()
    {
        const string content = @"
namespace X {
    public class Y {
        public void Run() { }
    }
}";
        var file = _tempFiles.CreateFileWithContent("File1.cs", content);
        var analyser = new CognitiveCodeAnalyser();

        var metrics = await analyser.AnalyseFilesAsync([file], _configuration);

        Assert.That(metrics, Has.Count.EqualTo(1));
    }

    [Test]
    public void AnalyseSourceFiles_WithCouplingEnabled_ReportsCouplingPhases()
    {
        const string content = @"
namespace X {
    public class Y {
        public void Run() { }
    }
}";
        _tempFiles.CreateFileWithContent("File1.cs", content);
        _tempFiles.CreateFileWithContent("File2.cs", content);

        var configuration = new CognitiveConfiguration { ShowCouplingMetrics = true };
        var collector = new AnalysisProgressCollector();
        var facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(),
            new CognitiveCodeAnalyser(),
            configuration,
            new ScoreCalculator(),
            new CoberturaReader(),
            new ClassCouplingAnalyser()
        );

        var files = facade.FindSourceFiles(_tempFiles.tmpDirectory, collector);
        facade.AnalyseSourceFiles(files, configuration, collector);

        var reports = collector.Reports;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reports.Any(r => r.Phase == AnalysisProgressPhase.AnalysingCoupling), Is.True);
            Assert.That(reports.Any(r => r.Phase == AnalysisProgressPhase.CouplingCompleted), Is.True);
        }
    }

    [Test]
    public void AnalyseSourceFiles_PassesProgressToAnalyser()
    {
        const string content = @"
namespace X {
    public class Y {
        public void Run() { }
    }
}";
        _tempFiles.CreateFileWithContent("File1.cs", content);
        _tempFiles.CreateFileWithContent("File2.cs", content);

        var collector = new AnalysisProgressCollector();
        var facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(),
            new CognitiveCodeAnalyser(),
            _configuration,
            new ScoreCalculator(),
            new CoberturaReader(),
            new ClassCouplingAnalyser()
        );

        var files = facade.FindSourceFiles(_tempFiles.tmpDirectory, collector);
        facade.AnalyseSourceFiles(files, _configuration, collector);

        var reports = collector.Reports;
        using (Assert.EnterMultipleScope())
        {
            Assert.That(reports.Any(r => r.Phase == AnalysisProgressPhase.SearchingFiles), Is.True);
            Assert.That(reports.Any(r => r.Phase == AnalysisProgressPhase.SearchCompleted && r.TotalFiles == 2), Is.True);
            Assert.That(reports.Any(r => r.Phase == AnalysisProgressPhase.AnalysingFiles), Is.True);
            Assert.That(reports.Any(r => r.Phase == AnalysisProgressPhase.AnalysisCompleted), Is.True);
        }
    }
}
