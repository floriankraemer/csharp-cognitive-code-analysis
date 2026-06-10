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
    private int _lastAnalysisProcessed = -1;
    private int _lastReportProcessed = -1;

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
        if (!ApplyProgress(update))
        {
            return;
        }

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
        if (State.ReportGeneratedMessage is { } reportMessage)
        {
            AnsiConsole.MarkupLine($"[green]{Markup.Escape(reportMessage)}[/]");
            State.ReportGeneratedMessage = null;
        }
    }

    public void FinalizeSession()
    {
        lock (_lock)
        {
            if (_context == null)
            {
                return;
            }

            if (_searchTask != null)
            {
                _searchTask.IsIndeterminate(false);
                _searchTask.MaxValue = 1;
                _searchTask.Description = State.SearchCompleteMessage ?? "Search complete";
                _searchTask.Value = 1;
            }

            if (_analysisTask != null && State.AnalysisMaxValue > 0)
            {
                _analysisTask.MaxValue = State.AnalysisMaxValue;
                _analysisTask.Description = State.AnalysisDescription ?? "Analysing files";
                _analysisTask.Value = State.AnalysisMaxValue;
            }

            if (_reportTask != null && State.ReportMaxValue > 0)
            {
                _reportTask.MaxValue = State.ReportMaxValue;
                _reportTask.Description = State.ReportDescription ?? "Writing report";
                _reportTask.Value = State.ReportMaxValue;
            }

            _context.Refresh();
        }
    }

    public bool ApplyProgress(AnalysisProgress update)
    {
        lock (_lock)
        {
            switch (update.Phase)
            {
                case AnalysisProgressPhase.SearchingFiles:
                    State.SearchStarted = true;
                    return true;

                case AnalysisProgressPhase.SearchCompleted:
                    State.SearchCompleted = true;
                    State.FoundFileCount = update.TotalFiles;
                    State.SearchCompleteMessage = $"Found {update.TotalFiles} C# file(s)";
                    return true;

                case AnalysisProgressPhase.AnalysingFiles:
                    if (update.ProcessedFiles < _lastAnalysisProcessed)
                    {
                        return false;
                    }

                    _lastAnalysisProcessed = update.ProcessedFiles;
                    State.AnalysisDescription = $"Analysing files ({update.ProcessedFiles}/{update.TotalFiles})";
                    State.AnalysisValue = update.ProcessedFiles;
                    State.AnalysisMaxValue = update.TotalFiles;
                    return true;

                case AnalysisProgressPhase.AnalysisCompleted:
                    _lastAnalysisProcessed = update.TotalFiles;
                    State.AnalysisCompleted = true;
                    State.AnalysisDescription = $"Analysing files ({update.ProcessedFiles}/{update.TotalFiles})";
                    State.AnalysisValue = update.TotalFiles;
                    State.AnalysisMaxValue = update.TotalFiles;
                    return true;

                case AnalysisProgressPhase.WritingReport:
                    if (update.ProcessedFiles < _lastReportProcessed)
                    {
                        return false;
                    }

                    _lastReportProcessed = update.ProcessedFiles;
                    State.ReportDescription = FormatReportDescription(update);
                    State.ReportValue = update.ProcessedFiles;
                    State.ReportMaxValue = update.TotalFiles;
                    return true;

                case AnalysisProgressPhase.ReportCompleted:
                    _lastReportProcessed = update.TotalFiles;
                    State.ReportCompleted = true;
                    State.ReportDescription = FormatReportDescription(update);
                    State.ReportValue = update.TotalFiles;
                    State.ReportMaxValue = update.TotalFiles;
                    return true;

                default:
                    return false;
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
                        _searchTask.IsIndeterminate(false);
                        _searchTask.MaxValue = 1;
                        _searchTask.Description = State.SearchCompleteMessage ?? "Search complete";
                        _searchTask.Value = 1;
                    }

                    break;

                case AnalysisProgressPhase.AnalysingFiles:
                    if (_analysisTask == null && State.AnalysisMaxValue > 0)
                    {
                        _analysisTask = _context.AddTask(
                            State.AnalysisDescription ?? "Analysing files",
                            maxValue: State.AnalysisMaxValue
                        );
                    }

                    if (_analysisTask != null)
                    {
                        _analysisTask.MaxValue = State.AnalysisMaxValue;
                        _analysisTask.Description = State.AnalysisDescription ?? "Analysing files";
                        _analysisTask.Value = State.AnalysisValue;
                    }

                    break;

                case AnalysisProgressPhase.AnalysisCompleted:
                    if (_analysisTask != null)
                    {
                        _analysisTask.MaxValue = State.AnalysisMaxValue;
                        _analysisTask.Description = State.AnalysisDescription ?? "Analysing files";
                        _analysisTask.Value = State.AnalysisMaxValue;
                    }

                    break;

                case AnalysisProgressPhase.WritingReport:
                    if (_reportTask == null && State.ReportMaxValue > 0)
                    {
                        _reportTask = _context.AddTask(
                            State.ReportDescription ?? "Writing report",
                            maxValue: State.ReportMaxValue
                        );
                    }

                    if (_reportTask != null)
                    {
                        _reportTask.MaxValue = State.ReportMaxValue;
                        _reportTask.Description = State.ReportDescription ?? "Writing report";
                        _reportTask.Value = State.ReportValue;
                    }

                    break;

                case AnalysisProgressPhase.ReportCompleted:
                    if (_reportTask != null)
                    {
                        _reportTask.MaxValue = State.ReportMaxValue;
                        _reportTask.Description = State.ReportDescription ?? "Writing report";
                        _reportTask.Value = State.ReportMaxValue;
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
