/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Collections.ObjectModel;
using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;
using CognitiveCodeAnalysisConsoleApp.Application;
using CognitiveCodeAnalysisConsoleApp.Commands;
using CognitiveCodeAnalysisConsoleApp.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysisConsoleApp.DependencyInjection;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp;

/// <summary>
/// <![CDATA[
/// https://spectreconsole.net/
/// https://spectreconsole.net/cli/tutorials/dependency-injection-in-cli-apps
/// ]]>
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        int? generateConfigExitCode = TryGenerateConfig(args);
        if (generateConfigExitCode.HasValue)
        {
            return generateConfigExitCode.Value;
        }

        var serviceCollection = new ServiceCollection();

        CognitiveConfiguration defaultConfig = ConfigurationLoader.Load();

        serviceCollection.AddSingleton(defaultConfig);
        serviceCollection.AddSingleton<SourceFileFinder>();
        serviceCollection.AddSingleton<CognitiveCodeAnalyser>();
        serviceCollection.AddSingleton<ScoreCalculator>();
        serviceCollection.AddSingleton<ClassCouplingAnalyser>();
        serviceCollection.AddSingleton<CognitiveAnalysisFacade>();
        serviceCollection.AddSingleton<ICoverageReader, AutoDetectCoverageReader>();

        serviceCollection.AddSingleton<IReport, HtmlReport>();
        serviceCollection.AddSingleton<IReport, ConsoleTextReport>();
        serviceCollection.AddSingleton<IReport, SarifReport>();
        serviceCollection.AddSingleton<IReport, GithubActionsReport>();
        serviceCollection.AddSingleton<IReport, GitlabCodeQualityReport>();
        serviceCollection.AddSingleton<IReport, CsvReport>();
        serviceCollection.AddSingleton<IReport, MarkdownReport>();
        serviceCollection.AddSingleton<IReport, JsonReport>();
        serviceCollection.AddSingleton<ReportCoordinator>();
        serviceCollection.AddSingleton<BaselineComparisonService>();
        serviceCollection.AddSingleton<AnalysisWorkflow>();
        serviceCollection.AddSingleton<IConsoleNotifier, SpectreConsoleNotifier>();
        serviceCollection.AddSingleton<IReportGenerationService, SpectreReportGenerationService>();
        serviceCollection.AddSingleton<AnalyseApplicationService>();

        // Create a type registrar and register any dependencies.
        // A type registrar is an adapter for a DI framework.
        var registrar = new TypeRegistrar(serviceCollection);

        return new CommandApp<AnalyseCommand>(registrar).Run(args);
    }

    private static int? TryGenerateConfig(string[] args)
    {
        int generateConfigIndex = Array.IndexOf(args, "--generate-config");
        if (generateConfigIndex < 0)
        {
            return null;
        }

        string? nextArg = generateConfigIndex + 1 < args.Length ? args[generateConfigIndex + 1] : null;
        string directory = nextArg is not null && !nextArg.StartsWith('-')
            ? nextArg
            : Directory.GetCurrentDirectory();
        string written = ConfigFileGenerator.Generate(directory);
        AnsiConsole.MarkupLine($"[green]Config file created:[/] {Markup.Escape(written)}");
        return 0;
    }
}
