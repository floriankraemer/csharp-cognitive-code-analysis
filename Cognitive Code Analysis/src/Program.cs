using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Commands;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace CognitiveCodeAnalysis;

public class Program
{
    public static int Main(string[] args)
    {
        var registrations = new ServiceCollection();

        CognitiveConfiguration defaultConfig = ConfigurationLoader.Load();
        registrations.AddSingleton(defaultConfig);

        // Register dependencies
        registrations.AddSingleton<FileFinder>();
        registrations.AddSingleton<CognitiveCodeAnalyser>();
        registrations.AddSingleton<ScoreCalculator>();
        registrations.AddSingleton<CognitiveAnalysisFacade>();

        // Create a type registrar and register any dependencies.
        // A type registrar is an adapter for a DI framework.
        var registrar = new TypeRegistrar(registrations);

        // Create a new command app with the registrar and run it with the provided arguments.
        var app = new CommandApp<AnalyseCommand>(registrar);

        return app.Run(args);
    }
}
