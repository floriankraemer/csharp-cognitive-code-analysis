/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysisConsoleApp.Infrastructure;

/// <summary>
/// Collects the timings and counts of one run for <c>--debug</c>. The phases are the <see cref="AnalysisProgressPhase"/>
/// values the analysis already reports; a phase lasts from its first report to the last report of it or of its
/// completion step. Phases that run side by side, such as cognitive and coupling analysis, can overlap.
/// </summary>
internal sealed class ExecutionSummary
{
    private readonly TimeProvider _clock;
    private readonly long _start;
    private readonly object _gate = new();
    private readonly List<AnalysisProgressPhase> _order = [];
    private readonly Dictionary<AnalysisProgressPhase, (long First, long Last)> _spans = [];
    private int _filesAnalysed;

    public ExecutionSummary(TimeProvider clock)
    {
        _clock = clock;
        _start = clock.GetTimestamp();
    }

    public int FilesFound { get; set; }

    public int MethodsAnalysed { get; set; }

    /// <summary>Wraps <paramref name="inner"/> so that every report it receives is also recorded.</summary>
    public IProgress<AnalysisProgress> Observe(IProgress<AnalysisProgress> inner) => new Observer(this, inner);

    public void WriteTo(TextWriter writer)
    {
        TimeSpan total = _clock.GetElapsedTime(_start);

        writer.WriteLine("Execution Summary:");
        writer.WriteLine($"  Total execution time: {Seconds(total)}");
        writer.WriteLine("Phase timings:");
        lock (_gate)
        {
            foreach (AnalysisProgressPhase phase in _order)
            {
                (long first, long last) = _spans[phase];
                writer.WriteLine($"    {phase}: {Seconds(_clock.GetElapsedTime(first, last))}");
            }

            writer.WriteLine("Statistics:");
            writer.WriteLine($"    Files found: {FilesFound}");
            writer.WriteLine($"    Files analysed: {_filesAnalysed}");
            writer.WriteLine($"    Files failed: {Math.Max(0, FilesFound - _filesAnalysed)}");
        }

        writer.WriteLine($"    Methods analysed: {MethodsAnalysed}");
    }

    private void Record(AnalysisProgress value)
    {
        long now = _clock.GetTimestamp();
        AnalysisProgressPhase phase = StartOf(value.Phase);

        lock (_gate)
        {
            _spans[phase] = _spans.TryGetValue(phase, out var span) ? (span.First, now) : (now, now);
            if (_spans.Count > _order.Count)
            {
                _order.Add(phase);
            }

            if (value.Phase is AnalysisProgressPhase.AnalysingFiles or AnalysisProgressPhase.AnalysisCompleted)
            {
                _filesAnalysed = Math.Max(_filesAnalysed, value.ProcessedFiles);
            }
        }
    }

    // A completion step belongs to the phase it completes.
    private static AnalysisProgressPhase StartOf(AnalysisProgressPhase phase) => phase switch
    {
        AnalysisProgressPhase.SearchCompleted => AnalysisProgressPhase.SearchingFiles,
        AnalysisProgressPhase.CompilationCompleted => AnalysisProgressPhase.CompilingSources,
        AnalysisProgressPhase.AnalysisCompleted => AnalysisProgressPhase.AnalysingFiles,
        AnalysisProgressPhase.CouplingCompleted => AnalysisProgressPhase.AnalysingCoupling,
        AnalysisProgressPhase.ScoresCalculated => AnalysisProgressPhase.CalculatingScores,
        AnalysisProgressPhase.CoverageApplied => AnalysisProgressPhase.ApplyingCoverage,
        AnalysisProgressPhase.BaselineCompared => AnalysisProgressPhase.ComparingBaseline,
        AnalysisProgressPhase.ReportCompleted => AnalysisProgressPhase.WritingReport,
        _ => phase,
    };

    private static string Seconds(TimeSpan span)
        => span.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture) + "s";

    private sealed class Observer(ExecutionSummary summary, IProgress<AnalysisProgress> inner) : IProgress<AnalysisProgress>
    {
        public void Report(AnalysisProgress value)
        {
            summary.Record(value);
            inner.Report(value);
        }
    }
}
