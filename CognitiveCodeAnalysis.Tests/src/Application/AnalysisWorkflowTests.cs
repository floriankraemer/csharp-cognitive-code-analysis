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

namespace CognitiveCodeAnalysis.Tests.Application;

public class AnalysisWorkflowTests
{
    [Test]
    public void Prepare_PopulatesConfigSource_OnPreparedAnalysis()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "cogcfg-workflow-" + Guid.NewGuid());
        var originalWorkingDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.CreateDirectory(tempDirectory);
            Directory.SetCurrentDirectory(tempDirectory);

            var workflow = CreateWorkflow();

            var prepared = workflow.Prepare(new AnalysisRequest(
                SourcePath: ".",
                ConfigFile: null,
                ReportType: "Html",
                BaselineFile: null,
                OutputFile: null,
                CoverageCobertura: null
            ));

            Assert.That(prepared.ConfigSource, Is.Not.Null);
            Assert.That(prepared.ConfigSource.IsDefault, Is.True);
            Assert.That(prepared.ConfigSource.Display, Is.EqualTo("Default"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalWorkingDirectory);

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }

    [Test]
    public void Prepare_ResolvesPathsAndDefaults()
    {
        var workflow = CreateWorkflow();

        var prepared = workflow.Prepare(new AnalysisRequest(
            SourcePath: ".",
            ConfigFile: null,
            ReportType: "Html",
            BaselineFile: null,
            OutputFile: null,
            CoverageCobertura: null
        ));

        Assert.That(prepared.AbsoluteSourcePath, Is.EqualTo(Path.GetFullPath(".")));
        Assert.That(prepared.ReportType, Is.EqualTo("Html"));
        Assert.That(prepared.OutputFile, Is.EqualTo(Path.GetFullPath("cognitive-analysis-report")));
        Assert.That(prepared.IsConsoleTextReport, Is.False);
    }

    [Test]
    public void Prepare_RecognizesConsoleTextReport()
    {
        var workflow = CreateWorkflow();

        var prepared = workflow.Prepare(new AnalysisRequest(
            SourcePath: null,
            ConfigFile: null,
            ReportType: "consoletext",
            BaselineFile: null,
            OutputFile: "out.txt",
            CoverageCobertura: null
        ));

        Assert.That(prepared.IsConsoleTextReport, Is.True);
        Assert.That(prepared.OutputFile, Is.EqualTo(Path.GetFullPath("out.txt")));
    }

    [Test]
    public void Prepare_ResolvesRelativeBaselineAndOutputPaths()
    {
        var workflow = CreateWorkflow();
        var baseline = Path.Combine(".", "baseline.json");
        var output = Path.Combine(".", "report.html");

        var prepared = workflow.Prepare(new AnalysisRequest(
            SourcePath: ".",
            ConfigFile: null,
            ReportType: "Html",
            BaselineFile: baseline,
            OutputFile: output,
            CoverageCobertura: Path.Combine(".", "coverage.xml")
        ));

        Assert.That(prepared.BaselineFile, Is.EqualTo(Path.GetFullPath(baseline)));
        Assert.That(prepared.OutputFile, Is.EqualTo(Path.GetFullPath(output)));
        Assert.That(prepared.CoverageCobertura, Is.EqualTo(Path.GetFullPath(Path.Combine(".", "coverage.xml"))));
    }

    [Test]
    public void ApplyCoverageIfRequested_WhenPathMissing_ReturnsSuccess()
    {
        var workflow = CreateWorkflow();

        var result = workflow.ApplyCoverageIfRequested(
            coverageFilePath: null,
            metricsCollection: new CognitiveMetricsCollection()
        );

        Assert.That(result.Success, Is.True);
        Assert.That(result.WarningMessage, Is.Null);
    }

    [Test]
    public void ApplyCoverageIfRequested_WhenFileMissing_ReturnsWarningMessage()
    {
        var workflow = CreateWorkflow();

        var result = workflow.ApplyCoverageIfRequested(
            coverageFilePath: "missing-coverage-file.xml",
            metricsCollection: new CognitiveMetricsCollection()
        );

        Assert.That(result.Success, Is.False);
        Assert.That(result.WarningMessage, Does.Contain("Coverage file not found"));
    }

    [Test]
    public void CompareBaselineIfRequested_WhenBaselineMissing_ReturnsNull()
    {
        var workflow = CreateWorkflow();

        var comparison = workflow.CompareBaselineIfRequested(
            baselineFile: null,
            metricsCollection: new CognitiveMetricsCollection()
        );

        Assert.That(comparison, Is.Null);
    }

    [Test]
    public void CompareBaselineIfRequested_WithBaselineFile_ReturnsComparison()
    {
        var workflow = CreateWorkflow();
        var baselineMetrics = new CognitiveMetrics(
            methodName: "Run",
            className: "App.Service",
            filePath: "src/Service.cs",
            methodSignature: "void Run()",
            methodLineNumber: 5
        );
        baselineMetrics.totalScore = 1.0;

        var snapshot = BaselineSnapshotFactory.FromMetricsCollection(new CognitiveMetricsCollection { baselineMetrics });
        var path = Path.Combine(Path.GetTempPath(), "workflow-baseline-" + Guid.NewGuid() + ".json");

        try
        {
            File.WriteAllText(path, BaselineLoader.Serialize(snapshot));

            var currentMetrics = new CognitiveMetrics(
                methodName: "Run",
                className: "App.Service",
                filePath: "src/Service.cs",
                methodSignature: "void Run()",
                methodLineNumber: 5
            );
            currentMetrics.totalScore = 3.0;

            var comparison = workflow.CompareBaselineIfRequested(
                baselineFile: path,
                metricsCollection: new CognitiveMetricsCollection { currentMetrics }
            );

            Assert.That(comparison, Is.Not.Null);
            Assert.That(comparison!.TryGetMethodComparison(currentMetrics, out MethodMetricsComparison? methodComparison), Is.True);
            Assert.That(methodComparison!.TotalScore.Delta, Is.EqualTo(2.0).Within(0.0001));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void GenerateReport_DelegatesToCoordinator()
    {
        var fakeReport = new FakeReport();
        var coordinator = new ReportCoordinator([fakeReport]);
        var workflow = CreateWorkflow(reportCoordinator: coordinator);
        var configuration = new CognitiveConfiguration();
        var metrics = new CognitiveMetricsCollection();

        workflow.GenerateReport(
            reportType: "Html",
            outputFile: "report.html",
            configuration: configuration,
            metricsCollection: metrics
        );

        Assert.That(fakeReport.RenderCalls, Is.EqualTo(1));
        Assert.That(fakeReport.LastOutputFile, Is.EqualTo("report.html"));
    }

    private static AnalysisWorkflow CreateWorkflow(ReportCoordinator? reportCoordinator = null)
    {
        var facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(),
            new CognitiveCodeAnalyser(),
            new CognitiveConfiguration(),
            new ScoreCalculator(),
            new CoberturaReader(),
            new ClassCouplingAnalyser()
        );

        reportCoordinator ??= new ReportCoordinator(Array.Empty<IReport>());

        return new AnalysisWorkflow(
            facade,
            new BaselineComparisonService(),
            reportCoordinator
        );
    }

    private sealed class FakeReport : IReport
    {
        public string Name => "Html";

        public int RenderCalls { get; private set; }

        public string? LastOutputFile { get; private set; }

        public void RenderMetrics(
            string outputFile,
            CognitiveMetricsCollection metricsCollection,
            CognitiveConfiguration configuration,
            CognitiveBaselineComparison? baselineComparison = null,
            IProgress<AnalysisProgress>? progress = null
        ) {
            RenderCalls++;
            LastOutputFile = outputFile;
        }
    }
}
