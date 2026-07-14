/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.Progress;

public sealed class SpectreAnalysisProgressReporter
{
    private const long RefreshThrottleMilliseconds = 75;

    private readonly object _lock = new();
    private ProgressContext? _context;
    private ProgressTask? _searchTask;
    private ProgressTask? _compileTask;
    private ProgressTask? _analysisTask;
    private ProgressTask? _couplingTask;
    private ProgressTask? _scoresTask;
    private ProgressTask? _coverageTask;
    private ProgressTask? _baselineTask;
    private ProgressTask? _reportTask;
    private int _lastCompileProcessed = -1;
    private int _lastAnalysisProcessed = -1;
    private int _lastCouplingProcessed = -1;
    private int _lastScoresProcessed = -1;
    private int _lastCoverageProcessed = -1;
    private int _lastBaselineProcessed = -1;
    private int _lastReportProcessed = -1;
    private long _lastRefreshTick = long.MinValue;

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

            FinalizeTask(_compileTask, State.CompileDescription ?? "Compiling sources", State.CompileMaxValue, State.CompileMaxValue);
            FinalizeTask(_analysisTask, State.AnalysisDescription ?? "Analysing files", State.AnalysisMaxValue, State.AnalysisMaxValue);
            FinalizeTask(_couplingTask, State.CouplingDescription ?? "Analysing coupling", State.CouplingMaxValue, State.CouplingMaxValue);
            FinalizeTask(_scoresTask, State.ScoresDescription ?? "Calculating scores", State.ScoresMaxValue, State.ScoresMaxValue);
            FinalizeTask(_coverageTask, State.CoverageDescription ?? "Applying coverage", State.CoverageMaxValue, State.CoverageMaxValue);
            FinalizeTask(_baselineTask, State.BaselineDescription ?? "Comparing baseline", State.BaselineMaxValue, State.BaselineMaxValue);
            FinalizeTask(_reportTask, State.ReportDescription ?? "Writing report", State.ReportMaxValue, State.ReportMaxValue);

            _context.Refresh();
        }
    }

    private static void FinalizeTask(ProgressTask? task, string description, double maxValue, double value)
    {
        if (task == null || maxValue <= 0)
        {
            return;
        }

        task.MaxValue = maxValue;
        task.Description = description;
        task.Value = value;
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

                case AnalysisProgressPhase.CompilingSources:
                    return ApplyIncremental(
                        update,
                        ref _lastCompileProcessed,
                        (processed, total) =>
                        {
                            State.CompileDescription = $"Compiling sources ({processed}/{total})";
                            State.CompileValue = processed;
                            State.CompileMaxValue = total;
                        });

                case AnalysisProgressPhase.CompilationCompleted:
                    _lastCompileProcessed = update.TotalFiles;
                    State.CompileCompleted = true;
                    State.CompileDescription = $"Compiling sources ({update.TotalFiles}/{update.TotalFiles})";
                    State.CompileValue = update.TotalFiles;
                    State.CompileMaxValue = update.TotalFiles;
                    return true;

                case AnalysisProgressPhase.AnalysingFiles:
                    return ApplyIncremental(
                        update,
                        ref _lastAnalysisProcessed,
                        (processed, total) =>
                        {
                            State.AnalysisDescription = $"Analysing files ({processed}/{total})";
                            State.AnalysisValue = processed;
                            State.AnalysisMaxValue = total;
                        });

                case AnalysisProgressPhase.AnalysisCompleted:
                    _lastAnalysisProcessed = update.TotalFiles;
                    State.AnalysisCompleted = true;
                    State.AnalysisDescription = $"Analysing files ({update.ProcessedFiles}/{update.TotalFiles})";
                    State.AnalysisValue = update.TotalFiles;
                    State.AnalysisMaxValue = update.TotalFiles;
                    return true;

                case AnalysisProgressPhase.AnalysingCoupling:
                    return ApplyIncremental(
                        update,
                        ref _lastCouplingProcessed,
                        (processed, total) =>
                        {
                            State.CouplingDescription = $"Analysing coupling ({processed}/{total})";
                            State.CouplingValue = processed;
                            State.CouplingMaxValue = total;
                        });

                case AnalysisProgressPhase.CouplingCompleted:
                    _lastCouplingProcessed = update.TotalFiles;
                    State.CouplingCompleted = true;
                    State.CouplingDescription = $"Analysing coupling ({update.TotalFiles}/{update.TotalFiles})";
                    State.CouplingValue = update.TotalFiles;
                    State.CouplingMaxValue = update.TotalFiles;
                    return true;

                case AnalysisProgressPhase.CalculatingScores:
                    return ApplyIncremental(
                        update,
                        ref _lastScoresProcessed,
                        (processed, total) =>
                        {
                            State.ScoresDescription = $"Calculating scores ({processed}/{total})";
                            State.ScoresValue = processed;
                            State.ScoresMaxValue = total;
                        });

                case AnalysisProgressPhase.ScoresCalculated:
                    _lastScoresProcessed = update.TotalFiles;
                    State.ScoresCompleted = true;
                    State.ScoresDescription = $"Calculating scores ({update.TotalFiles}/{update.TotalFiles})";
                    State.ScoresValue = update.TotalFiles;
                    State.ScoresMaxValue = update.TotalFiles;
                    return true;

                case AnalysisProgressPhase.ApplyingCoverage:
                    return ApplyIncremental(
                        update,
                        ref _lastCoverageProcessed,
                        (processed, total) =>
                        {
                            State.CoverageDescription = $"Applying coverage ({processed}/{total})";
                            State.CoverageValue = processed;
                            State.CoverageMaxValue = total;
                        });

                case AnalysisProgressPhase.CoverageApplied:
                    _lastCoverageProcessed = update.TotalFiles;
                    State.CoverageCompleted = true;
                    State.CoverageDescription = $"Applying coverage ({update.TotalFiles}/{update.TotalFiles})";
                    State.CoverageValue = update.TotalFiles;
                    State.CoverageMaxValue = update.TotalFiles;
                    return true;

                case AnalysisProgressPhase.ComparingBaseline:
                    return ApplyIncremental(
                        update,
                        ref _lastBaselineProcessed,
                        (processed, total) =>
                        {
                            State.BaselineDescription = $"Comparing baseline ({processed}/{total})";
                            State.BaselineValue = processed;
                            State.BaselineMaxValue = total;
                        });

                case AnalysisProgressPhase.BaselineCompared:
                    _lastBaselineProcessed = update.TotalFiles;
                    State.BaselineCompleted = true;
                    State.BaselineDescription = $"Comparing baseline ({update.TotalFiles}/{update.TotalFiles})";
                    State.BaselineValue = update.TotalFiles;
                    State.BaselineMaxValue = update.TotalFiles;
                    return true;

                case AnalysisProgressPhase.WritingReport:
                    return ApplyIncremental(
                        update,
                        ref _lastReportProcessed,
                        (processed, total) =>
                        {
                            State.ReportDescription = FormatReportDescription(update.ReportName, processed, total);
                            State.ReportValue = processed;
                            State.ReportMaxValue = total;
                        });

                case AnalysisProgressPhase.ReportCompleted:
                    _lastReportProcessed = update.TotalFiles;
                    State.ReportCompleted = true;
                    State.ReportDescription = FormatReportDescription(update.ReportName, update.TotalFiles, update.TotalFiles);
                    State.ReportValue = update.TotalFiles;
                    State.ReportMaxValue = update.TotalFiles;
                    return true;

                default:
                    return false;
            }
        }
    }

    private static bool ApplyIncremental(
        AnalysisProgress update,
        ref int lastProcessed,
        Action<int, int> applyState
    ) {
        if (update.ProcessedFiles < lastProcessed)
        {
            return false;
        }

        lastProcessed = update.ProcessedFiles;
        applyState(update.ProcessedFiles, update.TotalFiles);
        return true;
    }

    private static string FormatReportDescription(string? reportName, int processed, int total)
    {
        string name = reportName ?? "report";
        return $"Writing {name} report ({processed}/{total})";
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

                case AnalysisProgressPhase.CompilingSources:
                    EnsureTask(ref _compileTask, State.CompileDescription ?? "Compiling sources", State.CompileMaxValue);
                    UpdateTask(_compileTask, State.CompileDescription, State.CompileMaxValue, State.CompileValue);
                    break;

                case AnalysisProgressPhase.CompilationCompleted:
                    UpdateTask(_compileTask, State.CompileDescription, State.CompileMaxValue, State.CompileMaxValue);
                    break;

                case AnalysisProgressPhase.AnalysingFiles:
                    EnsureTask(ref _analysisTask, State.AnalysisDescription ?? "Analysing files", State.AnalysisMaxValue);
                    UpdateTask(_analysisTask, State.AnalysisDescription, State.AnalysisMaxValue, State.AnalysisValue);
                    break;

                case AnalysisProgressPhase.AnalysisCompleted:
                    UpdateTask(_analysisTask, State.AnalysisDescription, State.AnalysisMaxValue, State.AnalysisMaxValue);
                    break;

                case AnalysisProgressPhase.AnalysingCoupling:
                    EnsureTask(ref _couplingTask, State.CouplingDescription ?? "Analysing coupling", State.CouplingMaxValue, expireThrottle: true);
                    UpdateTask(_couplingTask, State.CouplingDescription, State.CouplingMaxValue, State.CouplingValue);
                    break;

                case AnalysisProgressPhase.CouplingCompleted:
                    UpdateTask(_couplingTask, State.CouplingDescription, State.CouplingMaxValue, State.CouplingMaxValue);
                    break;

                case AnalysisProgressPhase.CalculatingScores:
                    EnsureTask(ref _scoresTask, State.ScoresDescription ?? "Calculating scores", State.ScoresMaxValue, expireThrottle: true);
                    UpdateTask(_scoresTask, State.ScoresDescription, State.ScoresMaxValue, State.ScoresValue);
                    break;

                case AnalysisProgressPhase.ScoresCalculated:
                    UpdateTask(_scoresTask, State.ScoresDescription, State.ScoresMaxValue, State.ScoresMaxValue);
                    break;

                case AnalysisProgressPhase.ApplyingCoverage:
                    EnsureTask(ref _coverageTask, State.CoverageDescription ?? "Applying coverage", State.CoverageMaxValue, expireThrottle: true);
                    UpdateTask(_coverageTask, State.CoverageDescription, State.CoverageMaxValue, State.CoverageValue);
                    break;

                case AnalysisProgressPhase.CoverageApplied:
                    UpdateTask(_coverageTask, State.CoverageDescription, State.CoverageMaxValue, State.CoverageMaxValue);
                    break;

                case AnalysisProgressPhase.ComparingBaseline:
                    EnsureTask(ref _baselineTask, State.BaselineDescription ?? "Comparing baseline", State.BaselineMaxValue, expireThrottle: true);
                    UpdateTask(_baselineTask, State.BaselineDescription, State.BaselineMaxValue, State.BaselineValue);
                    break;

                case AnalysisProgressPhase.BaselineCompared:
                    UpdateTask(_baselineTask, State.BaselineDescription, State.BaselineMaxValue, State.BaselineMaxValue);
                    break;

                case AnalysisProgressPhase.WritingReport:
                    EnsureTask(ref _reportTask, State.ReportDescription ?? "Writing report", State.ReportMaxValue, expireThrottle: true);
                    UpdateTask(_reportTask, State.ReportDescription, State.ReportMaxValue, State.ReportValue);
                    break;

                case AnalysisProgressPhase.ReportCompleted:
                    UpdateTask(_reportTask, State.ReportDescription, State.ReportMaxValue, State.ReportMaxValue);
                    break;
            }

            if (ShouldRefresh(update.Phase))
            {
                _context.Refresh();
            }
        }
    }

    private void EnsureTask(
        ref ProgressTask? task,
        string description,
        double maxValue,
        bool expireThrottle = false
    ) {
        if (task == null && maxValue > 0)
        {
            task = _context!.AddTask(description, maxValue: maxValue);
            if (expireThrottle)
            {
                _lastRefreshTick = 0;
            }
        }
    }

    private static void UpdateTask(ProgressTask? task, string? description, double maxValue, double value)
    {
        if (task == null)
        {
            return;
        }

        task.MaxValue = maxValue;
        task.Description = description ?? task.Description;
        task.Value = value;
    }

    private bool ShouldRefresh(AnalysisProgressPhase phase)
    {
        bool isIncremental = phase
            is AnalysisProgressPhase.CompilingSources
            or AnalysisProgressPhase.AnalysingFiles
            or AnalysisProgressPhase.AnalysingCoupling
            or AnalysisProgressPhase.CalculatingScores
            or AnalysisProgressPhase.ApplyingCoverage
            or AnalysisProgressPhase.ComparingBaseline
            or AnalysisProgressPhase.WritingReport;
        if (!isIncremental)
        {
            _lastRefreshTick = Environment.TickCount64;
            return true;
        }

        long now = Environment.TickCount64;
        if (now - _lastRefreshTick < RefreshThrottleMilliseconds)
        {
            return false;
        }

        _lastRefreshTick = now;
        return true;
    }

    private void WriteFallbackLine(AnalysisProgress update)
    {
        switch (update.Phase)
        {
            case AnalysisProgressPhase.SearchingFiles:
                AnsiConsole.MarkupLine("[grey]Searching for C# files...[/]");
                break;

            case AnalysisProgressPhase.CompilingSources when update.ProcessedFiles == 0:
                AnsiConsole.MarkupLine($"[grey]Compiling {update.TotalFiles} source file(s)...[/]");
                break;

            case AnalysisProgressPhase.AnalysingFiles when update.ProcessedFiles == 0:
                AnsiConsole.MarkupLine($"[grey]Analysing {update.TotalFiles} file(s)...[/]");
                break;

            case AnalysisProgressPhase.AnalysingCoupling when update.ProcessedFiles == 0:
                AnsiConsole.MarkupLine($"[grey]Analysing coupling of {update.TotalFiles} unit(s)...[/]");
                break;

            case AnalysisProgressPhase.CalculatingScores when update.ProcessedFiles == 0:
                AnsiConsole.MarkupLine($"[grey]Calculating scores for {update.TotalFiles} method(s)...[/]");
                break;

            case AnalysisProgressPhase.ApplyingCoverage when update.ProcessedFiles == 0:
                AnsiConsole.MarkupLine($"[grey]Applying coverage to {update.TotalFiles} method(s)...[/]");
                break;

            case AnalysisProgressPhase.ComparingBaseline when update.ProcessedFiles == 0:
                AnsiConsole.MarkupLine($"[grey]Comparing baseline for {update.TotalFiles} method(s)...[/]");
                break;

            case AnalysisProgressPhase.WritingReport when update.ProcessedFiles == 0:
                AnsiConsole.MarkupLine($"[grey]Writing {Markup.Escape(update.ReportName ?? "report")} report ({update.TotalFiles} items)...[/]");
                break;

            case AnalysisProgressPhase.AnalysisCompleted:
            case AnalysisProgressPhase.ReportCompleted:
            case AnalysisProgressPhase.CompilationCompleted:
            case AnalysisProgressPhase.CouplingCompleted:
            case AnalysisProgressPhase.ScoresCalculated:
            case AnalysisProgressPhase.CoverageApplied:
            case AnalysisProgressPhase.BaselineCompared:
                break;
        }
    }
}
