/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.Infrastructure;

internal sealed class SpectreConsoleNotifier : IConsoleNotifier
{
    public void WriteError(string message)
        => WriteLine(
            $"[red]Error: {Markup.Escape(message)}[/]",
            $"Error: {message}"
        );

    public void WriteWarning(string message)
        => WriteLine(
            $"[yellow]Warning: {Markup.Escape(message)}[/]",
            $"Warning: {message}"
        );

    public void WriteNoSourceFilesFound(string absoluteSourcePath)
        => WriteLine(
            $"[yellow]No C# files found in {Markup.Escape(absoluteSourcePath)}.[/]",
            $"No C# files found in {absoluteSourcePath}."
        );

    public void WriteReportGenerated(string reportType, string fullPath)
        => WriteLine(
            $"[green]{Markup.Escape(reportType)} report generated:[/] {Markup.Escape(fullPath)}",
            $"{reportType} report generated: {fullPath}"
        );

    public void WriteConfigUsed(string configSourceDisplay)
        => WriteLine(
            $"[grey]Config: {Markup.Escape(configSourceDisplay)}[/]",
            $"Config: {configSourceDisplay}"
        );

    public void WriteConfigFileCreated(string fullPath)
        => WriteLine(
            $"[green]Config file created:[/] {Markup.Escape(fullPath)}",
            $"Config file created: {fullPath}"
        );

    private static void WriteLine(string markup, string plain)
    {
        if (AnsiConsole.Profile.Capabilities.Interactive)
        {
            AnsiConsole.MarkupLine(markup);
            return;
        }

        Console.WriteLine(plain);
    }
}
