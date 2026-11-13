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
            WarnAboutIgnoredArguments(context);

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

    // Unknown options and surplus arguments have always been ignored; say so instead of staying silent.
    private void WarnAboutIgnoredArguments(CommandContext context)
    {
        IEnumerable<string> ignored = context.Remaining.Parsed
            .SelectMany(group => group.Select(value => value is null ? group.Key : $"{group.Key} {value}")
                .DefaultIfEmpty(group.Key))
            .Concat(context.Remaining.Raw);

        if (ignored.Any())
        {
            consoleNotifier.WriteWarning($"Ignored unknown arguments: {string.Join(" ", ignored)}");
        }
    }
}
