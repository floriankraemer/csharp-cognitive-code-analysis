/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysisConsoleApp.Application;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;

using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Commands;

internal sealed class AnalyseCommand(
    AnalyseApplicationService applicationService,
    IConsoleNotifier consoleNotifier
) : Command<AnalyseCommandSettings> {

    private const int Success = 0;
    private const int Error = -1;

    public override int Execute(
        CommandContext context,
        AnalyseCommandSettings settings,
        CancellationToken cancellationToken
    ) {
        try
        {
            if (settings.GenerateConfig.IsSet)
            {
                string directory = string.IsNullOrWhiteSpace(settings.GenerateConfig.Value)
                    ? Directory.GetCurrentDirectory()
                    : settings.GenerateConfig.Value!;
                string written = ConfigFileGenerator.Generate(directory);
                consoleNotifier.WriteConfigFileCreated(written);
                return Success;
            }

            var request = AnalyseRequestMapper.FromSettings(settings);
            var result = applicationService.Run(request);

            return result.Outcome == AnalyseOutcome.Success ? Success : Error;
        }
        catch (Exception exception)
        {
            consoleNotifier.WriteError(exception.Message);
            return Error;
        }
    }
}
