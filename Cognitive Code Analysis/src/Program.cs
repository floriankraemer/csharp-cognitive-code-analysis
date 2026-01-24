using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Commands;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace CognitiveCodeAnalysis;

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
        var serviceCollection = new ServiceCollection();

        CognitiveConfiguration defaultConfig = ConfigurationLoader.Load();

        serviceCollection.AddSingleton(defaultConfig);
        serviceCollection.AddSingleton<SourceFileFinder>();
        serviceCollection.AddSingleton<CognitiveCodeAnalyser>();
        serviceCollection.AddSingleton<ScoreCalculator>();
        serviceCollection.AddSingleton<CognitiveAnalysisFacade>();
        serviceCollection.AddSingleton<ReportFactory>();

        // Create a type registrar and register any dependencies.
        // A type registrar is an adapter for a DI framework.
        var registrar = new TypeRegistrar(serviceCollection);

        // Create a new command app with the registrar and run it with the provided arguments.
        var app = new CommandApp<AnalyseCommand>(registrar);

        return app.Run(args);
    }
}
