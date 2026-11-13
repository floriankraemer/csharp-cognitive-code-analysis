/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysisConsoleApp.Infrastructure;

/// <summary>
/// What the command line knows about the terminal it runs in. Commands that ask questions only do so when a person can
/// answer them.
/// </summary>
public interface IConsoleEnvironment
{
    /// <summary>True when the output is an interactive terminal.</summary>
    bool OutputIsInteractive { get; }

    /// <summary>True when standard input is redirected, so no person is typing answers.</summary>
    bool InputIsRedirected { get; }
}
