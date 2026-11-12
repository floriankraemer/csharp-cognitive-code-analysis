/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Reports;

/// <summary>
/// Redirects the global <see cref="AnsiConsole"/> into a plain-text buffer (no colours, wide) for the lifetime of the scope.
/// </summary>
internal sealed class ConsoleOutputCapture : IDisposable
{
    private readonly IAnsiConsole _previous = AnsiConsole.Console;
    private readonly TextWriter _previousOut = Console.Out;
    private readonly TextWriter _previousError = Console.Error;
    private readonly StringWriter _writer = new();
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();

    public ConsoleOutputCapture(int width = 400, bool interactive = false)
    {
        Console.SetOut(_stdout);
        Console.SetError(_stderr);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Interactive = interactive ? InteractionSupport.Yes : InteractionSupport.No,
            Out = new AnsiConsoleOutput(_writer),
        });
        AnsiConsole.Console.Profile.Width = width;
    }

    /// <summary>Everything written through <see cref="Console.Out"/> (plain-text fallbacks).</summary>
    public string StdOut => _stdout.ToString();

    /// <summary>Everything written through <see cref="Console.Error"/> (diagnostic trace).</summary>
    public string StdErr => _stderr.ToString();

    /// <summary>Everything written through <see cref="AnsiConsole"/>.</summary>
    public string Text => _writer.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);

    public void Dispose()
    {
        AnsiConsole.Console = _previous;
        Console.SetOut(_previousOut);
        Console.SetError(_previousError);
    }
}
