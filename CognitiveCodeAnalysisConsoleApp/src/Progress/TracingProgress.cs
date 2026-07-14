/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Diagnostics;

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysisConsoleApp.Progress;

internal sealed class TracingProgress(IProgress<AnalysisProgress> inner, IAnalysisTracer tracer) : IProgress<AnalysisProgress>
{
    private AnalysisProgressPhase? _lastLoggedPhase;
    private int _lastLoggedProcessed = -1;
    private readonly Stopwatch _phaseStopwatch = Stopwatch.StartNew();

    public void Report(AnalysisProgress value)
    {
        LogPhaseTransition(value);
        inner.Report(value);
    }

    private void LogPhaseTransition(AnalysisProgress value)
    {
        bool isTerminal = value.Phase is AnalysisProgressPhase.SearchCompleted
            or AnalysisProgressPhase.CompilationCompleted
            or AnalysisProgressPhase.AnalysisCompleted
            or AnalysisProgressPhase.CouplingCompleted
            or AnalysisProgressPhase.ScoresCalculated
            or AnalysisProgressPhase.CoverageApplied
            or AnalysisProgressPhase.BaselineCompared
            or AnalysisProgressPhase.ReportCompleted;

        bool isPhaseStart = value.Phase != _lastLoggedPhase
            || (value.ProcessedFiles == 0 && _lastLoggedProcessed != 0);

        if (!isPhaseStart && !isTerminal)
        {
            return;
        }

        if (_lastLoggedPhase != null && _lastLoggedPhase != value.Phase)
        {
            tracer.Trace($"Phase {_lastLoggedPhase} finished ({_phaseStopwatch.ElapsedMilliseconds} ms)");
            _phaseStopwatch.Restart();
        }

        string details = FormatProgress(value);
        if (isTerminal)
        {
            tracer.Trace($"Phase {value.Phase} complete {details} ({_phaseStopwatch.ElapsedMilliseconds} ms)");
            _phaseStopwatch.Restart();
        }
        else
        {
            tracer.Trace($"Phase {value.Phase} {details}");
        }

        _lastLoggedPhase = value.Phase;
        _lastLoggedProcessed = value.ProcessedFiles;
    }

    private static string FormatProgress(AnalysisProgress value)
    {
        if (value.TotalFiles > 0 || value.ProcessedFiles > 0)
        {
            return $"({value.ProcessedFiles}/{value.TotalFiles})";
        }

        return value.ReportName is { } reportName ? $"[{reportName}]" : string.Empty;
    }
}
