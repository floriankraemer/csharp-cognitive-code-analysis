/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.Progress;

public sealed class SpectreAnalysisProgressReporter
{
    private readonly object _lock = new();
    private ProgressContext? _context;
    private ProgressTask? _searchTask;
    private ProgressTask? _analysisTask;
    private ProgressTask? _reportTask;
    private int _lastAnalysisProcessed;
    private int _lastReportProcessed;

    public SpectreAnalysisProgressState State { get; } = new();

    public void Attach(ProgressContext context)
    {
        lock (_lock)
        {
            _context = context;
        }
    }

    public void Report(AnalysisProgress update)
    {
        ApplyProgress(update);

        if (_context == null)
        {
            WriteFallbackLine(update);
            return;
        }

        UpdateSpectreTasks(update);
    }

    public void DeferReportGeneratedMessage(string reportType, string fullPath)
    {
        lock (_lock)
        {
            State.ReportGeneratedMessage = $"{reportType} report generated: {fullPath}";
        }
    }

    public void FlushPendingMessages()
    {
        if (State.SearchCompleteMessage is { } searchMessage)
        {
            AnsiConsole.MarkupLine($"[green]{Markup.Escape(searchMessage)}[/]");
            State.SearchCompleteMessage = null;
        }

        if (State.ReportGeneratedMessage is { } reportMessage)
        {
            AnsiConsole.MarkupLine($"[green]{Markup.Escape(reportMessage)}[/]");
            State.ReportGeneratedMessage = null;
        }
    }

    public void ApplyProgress(AnalysisProgress update)
    {
        lock (_lock)
        {
            switch (update.Phase)
            {
                case AnalysisProgressPhase.SearchingFiles:
                    State.SearchStarted = true;
                    break;

                case AnalysisProgressPhase.SearchCompleted:
                    State.SearchCompleted = true;
                    State.FoundFileCount = update.TotalFiles;
                    State.SearchCompleteMessage = $"Found {update.TotalFiles} C# file(s)";
                    break;

                case AnalysisProgressPhase.AnalysingFiles:
                    if (update.ProcessedFiles < _lastAnalysisProcessed)
                    {
                        break;
                    }

                    _lastAnalysisProcessed = update.ProcessedFiles;
                    State.AnalysisDescription = $"Analysing files ({update.ProcessedFiles}/{update.TotalFiles})";
                    State.AnalysisValue = update.ProcessedFiles;
                    State.AnalysisMaxValue = update.TotalFiles;
                    break;

                case AnalysisProgressPhase.AnalysisCompleted:
                    State.AnalysisCompleted = true;
                    State.AnalysisValue = update.ProcessedFiles;
                    State.AnalysisMaxValue = update.TotalFiles;
                    break;

                case AnalysisProgressPhase.WritingReport:
                    if (update.ProcessedFiles < _lastReportProcessed)
                    {
                        break;
                    }

                    _lastReportProcessed = update.ProcessedFiles;
                    State.ReportDescription = FormatReportDescription(update);
                    State.ReportValue = update.ProcessedFiles;
                    State.ReportMaxValue = update.TotalFiles;
                    break;

                case AnalysisProgressPhase.ReportCompleted:
                    State.ReportCompleted = true;
                    State.ReportValue = update.ProcessedFiles;
                    State.ReportMaxValue = update.TotalFiles;
                    break;
            }
        }
    }

    private static string FormatReportDescription(AnalysisProgress update)
    {
        string reportName = update.ReportName ?? "report";
        return $"Writing {reportName} report ({update.ProcessedFiles}/{update.TotalFiles})";
    }

    private void UpdateSpectreTasks(AnalysisProgress update)
    {
        lock (_lock)
        {
            if (_context == null)
            {
                return;
            }

            switch (update.Phase)
            {
                case AnalysisProgressPhase.SearchingFiles:
                    _searchTask ??= _context.AddTask("Searching for C# files...")
                        .IsIndeterminate();
                    break;

                case AnalysisProgressPhase.SearchCompleted:
                    if (_searchTask != null)
                    {
                        _searchTask.StopTask();
                        _searchTask.Value = _searchTask.MaxValue;
                    }

                    break;

                case AnalysisProgressPhase.AnalysingFiles:
                    if (_analysisTask == null && update.TotalFiles > 0)
                    {
                        _analysisTask = _context.AddTask(
                            State.AnalysisDescription ?? "Analysing files",
                            maxValue: update.TotalFiles
                        );
                    }

                    if (_analysisTask != null)
                    {
                        _analysisTask.Description = State.AnalysisDescription ?? "Analysing files";
                        _analysisTask.Value = update.ProcessedFiles;
                    }

                    break;

                case AnalysisProgressPhase.AnalysisCompleted:
                    _lastAnalysisProcessed = update.TotalFiles;

                    if (_analysisTask != null)
                    {
                        _analysisTask.MaxValue = update.TotalFiles;
                        _analysisTask.Value = update.TotalFiles;
                    }

                    break;

                case AnalysisProgressPhase.WritingReport:
                    if (_reportTask == null && update.TotalFiles > 0)
                    {
                        _reportTask = _context.AddTask(
                            State.ReportDescription ?? "Writing report",
                            maxValue: update.TotalFiles
                        );
                    }

                    if (_reportTask != null)
                    {
                        _reportTask.Description = State.ReportDescription ?? "Writing report";
                        _reportTask.Value = update.ProcessedFiles;
                    }

                    break;

                case AnalysisProgressPhase.ReportCompleted:
                    _lastReportProcessed = update.TotalFiles;

                    if (_reportTask != null)
                    {
                        _reportTask.MaxValue = update.TotalFiles;
                        _reportTask.Value = update.TotalFiles;
                    }

                    break;
            }

            _context.Refresh();
        }
    }

    private void WriteFallbackLine(AnalysisProgress update)
    {
        switch (update.Phase)
        {
            case AnalysisProgressPhase.SearchingFiles:
                AnsiConsole.MarkupLine("[grey]Searching for C# files...[/]");
                break;

            case AnalysisProgressPhase.AnalysingFiles when update.ProcessedFiles == 0:
                AnsiConsole.MarkupLine($"[grey]Analysing {update.TotalFiles} file(s)...[/]");
                break;

            case AnalysisProgressPhase.WritingReport when update.ProcessedFiles == 0:
                AnsiConsole.MarkupLine($"[grey]Writing {Markup.Escape(update.ReportName ?? "report")} report ({update.TotalFiles} items)...[/]");
                break;

            case AnalysisProgressPhase.AnalysisCompleted:
            case AnalysisProgressPhase.ReportCompleted:
                break;
        }
    }
}
