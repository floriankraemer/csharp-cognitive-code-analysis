/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;

using Spectre.Console;
using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Commands;

/// <summary>
/// Creates a configuration file with the built-in defaults. In an interactive console the main settings are asked for
/// first; <c>--silent</c> or a non-interactive console writes the defaults.
/// </summary>
internal sealed class InitCommand(IConsoleNotifier consoleNotifier) : Command<InitCommandSettings>
{
    private const int Success = 0;
    private const int Error = -1;

    public override int Execute(
        CommandContext context,
        InitCommandSettings settings,
        CancellationToken cancellationToken
    ) {
        try
        {
            string target = ConfigInitializer.PrepareTarget(settings.Path, settings.Force);
            ConfigInitAnswers defaults = ConfigInitAnswers.FromConfiguration(CognitiveConfigurationDefaults.Create());
            ConfigInitAnswers answers = ShouldAsk(settings)
                ? ConfigInitPrompter.Ask(defaults)
                : defaults;

            ConfigInitializer.Write(target, ConfigInitializer.Render(answers), settings.Force);
            consoleNotifier.WriteInitConfigCreated(target);
            return Success;
        }
        catch (ConfigFileException exception)
        {
            consoleNotifier.WriteError(exception.Message);
            return Error;
        }
    }

    private static bool ShouldAsk(InitCommandSettings settings) =>
        !settings.Silent && AnsiConsole.Profile.Capabilities.Interactive;
}
