/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;
using CognitiveCodeAnalysisConsoleApp.Application;
using CognitiveCodeAnalysisConsoleApp.Commands;
using CognitiveCodeAnalysisConsoleApp.DependencyInjection;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;

using Microsoft.Extensions.DependencyInjection;

using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Commands;

public class AnalyseCommandCliIntegrationTests
{
    private string _tempDirectory = null!;
    private string _outputFile = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"cca-cli-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        File.WriteAllText(
            Path.Combine(_tempDirectory, "Sample.cs"),
            """
            namespace Samples;

            public class Sample
            {
                public void M()
                {
                    if (true)
                    {
                        return;
                    }
                }
            }
            """
        );

        _outputFile = Path.Combine(_tempDirectory, "report.html");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [TestCase("-f", "Html")]
    [TestCase("--report-format", "Html")]
    public void Run_WithReportFormatAndOutputOptions_WritesReportFile(string formatFlag, string formatValue)
    {
        var registrar = CreateRegistrar();
        var app = new CommandApp<AnalyseCommand>(registrar);

        int exitCode = app.Run(
        [
            _tempDirectory,
            formatFlag,
            formatValue,
            "-o",
            _outputFile,
        ]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(File.Exists(_outputFile), Is.True, "Expected report file to be written.");
        Assert.That(File.ReadAllText(_outputFile), Does.Contain("<html").IgnoreCase);
    }

    private static TypeRegistrar CreateRegistrar()
    {
        var services = new ServiceCollection();

        CognitiveConfiguration defaultConfig = ConfigurationLoader.Load();

        services.AddSingleton(defaultConfig);
        services.AddSingleton<SourceFileFinder>();
        services.AddSingleton<CognitiveCodeAnalyser>();
        services.AddSingleton<ScoreCalculator>();
        services.AddSingleton<ClassCouplingAnalyser>();
        services.AddSingleton<CognitiveAnalysisFacade>();
        services.AddSingleton<ICoverageReader, AutoDetectCoverageReader>();
        services.AddSingleton<IReport, HtmlReport>();
        services.AddSingleton<ReportCoordinator>();
        services.AddSingleton<BaselineComparisonService>();
        services.AddSingleton<AnalysisWorkflow>();
        services.AddSingleton<IConsoleNotifier, NullConsoleNotifier>();
        services.AddSingleton<IReportGenerationService, SpectreReportGenerationService>();
        services.AddSingleton<AnalyseApplicationService>();
        services.AddSingleton<AnalyseCommand>();

        return new TypeRegistrar(services);
    }

    private sealed class NullConsoleNotifier : IConsoleNotifier
    {
        public void WriteError(string message) { }

        public void WriteWarning(string message) { }

        public void WriteNoSourceFilesFound(string absoluteSourcePath) { }

        public void WriteReportGenerated(string reportType, string fullPath) { }

        public void WriteConfigUsed(string configSourceDisplay) { }
    }
}
