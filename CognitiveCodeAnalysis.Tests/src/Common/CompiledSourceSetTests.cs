/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Common;
using CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.Common;

public class CompiledSourceSetTests
{
    private TempFiles _tempFiles = null!;

    [SetUp]
    public void SetUp() => _tempFiles = new TempFiles();

    [TearDown]
    public void TearDown() => _tempFiles.CleanUp();

    [Test]
    public async Task BuildAsync_WithProgress_ReportsCompilePhases()
    {
        _tempFiles.CreateFileWithContent("A.cs", "namespace A; public class One { }");
        _tempFiles.CreateFileWithContent("B.cs", "namespace B; public class Two { }");
        _tempFiles.CreateFileWithContent("C.cs", "namespace C; public class Three { }");

        var files = Directory.GetFiles(_tempFiles.tmpDirectory, "*.cs").OrderBy(f => f).ToList();
        var collector = new AnalysisProgressCollector();

        await CompiledSourceSet.BuildAsync(files, collector);

        var reports = collector.Reports;
        var compilingReports = reports.Where(r => r.Phase == AnalysisProgressPhase.CompilingSources).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(compilingReports, Is.Not.Empty);
            Assert.That(compilingReports[0].ProcessedFiles, Is.EqualTo(0));
            Assert.That(compilingReports[0].TotalFiles, Is.EqualTo(3));
            Assert.That(compilingReports.Select(r => r.ProcessedFiles), Does.Contain(3));
            Assert.That(reports.Any(r => r.Phase == AnalysisProgressPhase.CompilationCompleted), Is.True);
        }
    }
}
