/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Collections.ObjectModel;
using System.Reflection;
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
    /// <summary>
    /// The informational version of the build without the <c>+sha</c> suffix the SDK appends.
    /// </summary>
    internal static string Version { get; } =
        (typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
        .Split('+')[0];

    public static int Main(string[] args) => Run(args, new SpectreConsoleEnvironment());

    /// <summary>
    /// Runs the command line with the given console environment. Tests pass their own to drive interactive prompts.
    /// </summary>
    public static int Run(string[] args, IConsoleEnvironment consoleEnvironment)
    {
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
        serviceCollection.AddSingleton<IReport, CheckstyleReport>();
        serviceCollection.AddSingleton<IReport, JUnitReport>();
        serviceCollection.AddSingleton<IReport, CsvReport>();
        serviceCollection.AddSingleton<IReport, MarkdownReport>();
        serviceCollection.AddSingleton<IReport, JsonReport>();
        serviceCollection.AddSingleton<ReportCoordinator>();
        serviceCollection.AddSingleton<BaselineComparisonService>();
        serviceCollection.AddSingleton<AnalysisWorkflow>();
        serviceCollection.AddSingleton<IConsoleNotifier, SpectreConsoleNotifier>();
        serviceCollection.AddSingleton<IReportGenerationService, SpectreReportGenerationService>();
        serviceCollection.AddSingleton<AnalyseApplicationService>();
        serviceCollection.AddSingleton(TimeProvider.System);
        serviceCollection.AddSingleton(consoleEnvironment);

        // Create a type registrar and register any dependencies.
        // A type registrar is an adapter for a DI framework.
        var registrar = new TypeRegistrar(serviceCollection);

        // `analyse` stays the default command, so `CognitiveCodeAnalysis <path> ...` keeps working.
        var app = new CommandApp<AnalyseCommand>(registrar);
        app.Configure(configurator =>
        {
            // Spectre would otherwise keep the console of the first run for all later runs in the same process.
            configurator.ConfigureConsole(AnsiConsole.Console);
            configurator.SetApplicationVersion(Version);
            configurator
                .AddCommand<ChurnCommand>("churn")
                .WithDescription("Calculates the churn (cognitive score times number of git changes) of classes.");
            configurator
                .AddCommand<InitCommand>("init")
                .WithDescription("Creates a cognitive-metrics-settings.json, asking for the main settings unless --silent.");
        });

        return app.Run(args);
    }
}
