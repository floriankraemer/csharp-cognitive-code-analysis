/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Common;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Benchmarks;

[ShortRunJob]
[MemoryDiagnoser]
public class AnalysisBenchmarks
{
    [Params(100, 400, 800)]
    public int FileCount { get; set; }

    private string _corpusDirectory = string.Empty;
    private List<string> _files = [];
    private CompiledSourceSet _compiledSources = null!;
    private CognitiveMetricsCollection _metrics = null!;
    private CognitiveConfiguration _configuration = null!;
    private CognitiveAnalysisFacade _facade = null!;
    private HtmlReport _htmlReport = null!;
    private string _reportOutputPath = string.Empty;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _corpusDirectory = BenchmarkCorpusGenerator.Generate(FileCount);
        _files = BenchmarkCorpusGenerator.ListSourceFiles(_corpusDirectory);
        _configuration = CreateBenchmarkConfiguration();
        _facade = CreateFacade();
        _htmlReport = new HtmlReport();
        _reportOutputPath = Path.Combine(_corpusDirectory, "benchmark-report.html");

        _compiledSources = CompiledSourceSet.BuildAsync(_files).GetAwaiter().GetResult();
        _metrics = _facade.AnalyseSourceFiles(_files, _configuration);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        if (Directory.Exists(_corpusDirectory))
        {
            Directory.Delete(_corpusDirectory, recursive: true);
        }
    }

    [Benchmark(Description = "01 Build compiled source set (read + parse + compile)")]
    public async Task BuildCompiledSourceSet()
    {
        await CompiledSourceSet.BuildAsync(_files);
    }

    [Benchmark(Description = "02 Cognitive analysis only")]
    public CognitiveMetricsCollection CognitiveAnalysisOnly()
    {
        var analyser = new CognitiveCodeAnalyser();
        return analyser.AnalyseCompiled(_compiledSources, _configuration, progress: null);
    }

    [Benchmark(Description = "03 Coupling analysis only")]
    public IReadOnlyList<ClassCouplingMetrics> CouplingAnalysisOnly()
    {
        var couplingAnalyser = new ClassCouplingAnalyser();
        return couplingAnalyser.AnalyseCompiled(_compiledSources);
    }

    [Benchmark(Description = "04 Full analysis pipeline (compile + cognitive + coupling + scores)")]
    public CognitiveMetricsCollection FullAnalysisPipeline()
    {
        return _facade.AnalyseSourceFiles(_files, _configuration);
    }

    [Benchmark(Description = "05 Html report generation")]
    public void HtmlReportGeneration()
    {
        _htmlReport.RenderMetrics(
            outputFile: _reportOutputPath,
            metricsCollection: _metrics,
            configuration: _configuration,
            baselineComparison: null,
            progress: null
        );
    }

    private static CognitiveConfiguration CreateBenchmarkConfiguration()
    {
        return new CognitiveConfiguration
        {
            ScoreThreshold = -1,
            ShowOnlyMethodsExceedingThreshold = false,
            ShowHalsteadComplexity = true,
            ShowCyclomaticComplexity = true,
            ShowCouplingMetrics = true,
            GroupByClass = true,
        };
    }

    private static CognitiveAnalysisFacade CreateFacade()
    {
        return new CognitiveAnalysisFacade(
            sourceFileFinder: new SourceFileFinder(),
            analyser: new CognitiveCodeAnalyser(),
            cognitiveConfiguration: new CognitiveConfiguration(),
            calculator: new ScoreCalculator(),
            coverageReader: new CoberturaReader(),
            classCouplingAnalyser: new ClassCouplingAnalyser()
        );
    }
}
