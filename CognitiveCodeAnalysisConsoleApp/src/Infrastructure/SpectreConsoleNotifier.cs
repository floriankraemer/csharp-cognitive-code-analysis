/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.Infrastructure;

internal sealed class SpectreConsoleNotifier : IConsoleNotifier
{
    public void WriteError(string message)
        => AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(message)}[/]");

    public void WriteWarning(string message)
        => AnsiConsole.MarkupLine($"[yellow]Warning: {Markup.Escape(message)}[/]");

    public void WriteNoSourceFilesFound(string absoluteSourcePath)
        => AnsiConsole.MarkupLine($"[yellow]No C# files found in {Markup.Escape(absoluteSourcePath)}.[/]");

    public void WriteReportGenerated(string reportType, string fullPath)
        => AnsiConsole.MarkupLine($"[green]{Markup.Escape(reportType)} report generated:[/] {Markup.Escape(fullPath)}");
}
