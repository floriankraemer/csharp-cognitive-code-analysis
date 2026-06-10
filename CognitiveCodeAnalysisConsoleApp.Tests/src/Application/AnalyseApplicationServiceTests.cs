/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;
using CognitiveCodeAnalysisConsoleApp.Application;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;
using CognitiveCodeAnalysisConsoleApp.Progress;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Application;

public class AnalyseApplicationServiceTests
{
    private string _tempDirectory = null!;
    private RecordingConsoleNotifier _notifier = null!;
    private RecordingReportGenerationService _reportGenerationService = null!;
    private AnalysisWorkflow _workflow = null!;
    private AnalyseApplicationService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"cca-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);

        _notifier = new RecordingConsoleNotifier();
        _reportGenerationService = new RecordingReportGenerationService();

        var facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(),
            new CognitiveCodeAnalyser(),
            new CognitiveConfiguration(),
            new ScoreCalculator(),
            new CoberturaReader(),
            new ClassCouplingAnalyser()
        );

        _workflow = new AnalysisWorkflow(
            facade,
            new BaselineComparisonService(),
            new ReportCoordinator(Array.Empty<IReport>())
        );

        _service = new AnalyseApplicationService(
            _workflow,
            _reportGenerationService,
            _notifier
        );
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Run_WhenNoSourceFiles_ReturnsNoSourceFilesOutcome()
    {
        var result = _service.Run(CreateRequest(reportType: "Html"));

        Assert.That(result.Outcome, Is.EqualTo(AnalyseOutcome.NoSourceFiles));
        Assert.That(_notifier.NoSourceFilesMessages, Has.Count.EqualTo(1));
        Assert.That(_reportGenerationService.Calls, Is.EqualTo(0));
    }

    [Test]
    public void Run_WhenCoverageFails_ReturnsCoverageFailedOutcome()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "Sample.cs"), "class Sample { void M() { } }");

        var result = _service.Run(CreateRequest(
            reportType: "Html",
            coverageCobertura: "missing-coverage.xml"
        ));

        Assert.That(result.Outcome, Is.EqualTo(AnalyseOutcome.CoverageFailed));
        Assert.That(_notifier.WarningMessages, Has.Count.EqualTo(1));
        Assert.That(_reportGenerationService.Calls, Is.EqualTo(0));
    }

    [Test]
    public void Run_ConsoleText_DefersReportUntilAfterProgressSession()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "Sample.cs"), "class Sample { void M() { } }");

        var result = _service.Run(CreateRequest(reportType: "ConsoleText"));

        Assert.That(result.Outcome, Is.EqualTo(AnalyseOutcome.Success));
        Assert.That(_reportGenerationService.Calls, Is.EqualTo(1));
        Assert.That(_reportGenerationService.LastProgressReporter, Is.Null);
    }

    [Test]
    public void Run_NonConsoleText_GeneratesReportInsideProgressSession()
    {
        File.WriteAllText(Path.Combine(_tempDirectory, "Sample.cs"), "class Sample { void M() { } }");

        var result = _service.Run(CreateRequest(reportType: "Html"));

        Assert.That(result.Outcome, Is.EqualTo(AnalyseOutcome.Success));
        Assert.That(_reportGenerationService.Calls, Is.EqualTo(1));
        Assert.That(_reportGenerationService.LastProgressReporter, Is.Not.Null);
    }

    private AnalysisRequest CreateRequest(
        string reportType,
        string? coverageCobertura = null
    ) => new(
        SourcePath: _tempDirectory,
        ConfigFile: null,
        ReportType: reportType,
        BaselineFile: null,
        OutputFile: "report.out",
        CoverageCobertura: coverageCobertura
    );

    private sealed class RecordingConsoleNotifier : IConsoleNotifier
    {
        public List<string> WarningMessages { get; } = [];

        public List<string> NoSourceFilesMessages { get; } = [];

        public void WriteError(string message) { }

        public void WriteWarning(string message) => WarningMessages.Add(message);

        public void WriteNoSourceFilesFound(string absoluteSourcePath)
            => NoSourceFilesMessages.Add(absoluteSourcePath);

        public void WriteReportGenerated(string reportType, string fullPath) { }
    }

    private sealed class RecordingReportGenerationService : IReportGenerationService
    {
        public int Calls { get; private set; }

        public SpectreAnalysisProgressReporter? LastProgressReporter { get; private set; }

        public void GenerateReport(
            PreparedAnalysis prepared,
            CognitiveMetricsCollection metricsCollection,
            CognitiveBaselineComparison? baselineComparison,
            IProgress<AnalysisProgress>? progress = null,
            SpectreAnalysisProgressReporter? progressReporter = null
        ) {
            Calls++;
            LastProgressReporter = progressReporter;
        }
    }
}
