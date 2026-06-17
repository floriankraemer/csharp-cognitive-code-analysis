/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysisConsoleApp.Commands;

using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Commands;

/// <summary>
/// Test-only command that captures parsed <see cref="AnalyseCommandSettings"/> without running analysis.
/// </summary>
internal sealed class CliParseProbeCommand : Command<AnalyseCommandSettings>
{
    internal static AnalyseCommandSettings? Parsed { get; private set; }

    internal static void Reset() => Parsed = null;

    public override int Execute(
        CommandContext context,
        AnalyseCommandSettings settings,
        CancellationToken cancellationToken
    ) {
        Parsed = settings;
        return 0;
    }
}
