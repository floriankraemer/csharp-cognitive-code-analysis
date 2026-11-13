/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.Infrastructure;

/// <summary>
/// Reads the real console. Both values are read on every call, so they follow the console that is current.
/// </summary>
public sealed class SpectreConsoleEnvironment : IConsoleEnvironment
{
    public bool OutputIsInteractive => AnsiConsole.Profile.Capabilities.Interactive;

    public bool InputIsRedirected => Console.IsInputRedirected;
}
