/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.Progress;

internal static class SpectreProgressSession
{
    /// <summary>
    /// Synchronous IProgress adapter that invokes the reporter directly on the calling thread,
    /// avoiding the async thread-pool dispatch of <see cref="Progress{T}"/>. This ensures the
    /// Spectre progress state is always up-to-date before <see cref="SpectreAnalysisProgressReporter.FinalizeSession"/>
    /// is called, and prevents progress events from being lost when work completes faster than
    /// the thread pool can drain its queue.
    /// </summary>
    private sealed class SynchronousProgress(SpectreAnalysisProgressReporter reporter) : IProgress<AnalysisProgress>
    {
        public void Report(AnalysisProgress value) => reporter.Report(value);
    }

    public static void Run(Action<SpectreAnalysisProgressReporter, IProgress<AnalysisProgress>> action)
    {
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            var silentReporter = new SpectreAnalysisProgressReporter();
            var silentProgress = new SynchronousProgress(silentReporter);
            action(silentReporter, silentProgress);
            silentReporter.FinalizeSession();
            silentReporter.FlushPendingMessages();
            return;
        }

        SpectreAnalysisProgressReporter? reporter = null;

        AnsiConsole.Progress()
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn()
            )
            .Start(ctx =>
            {
                reporter = new SpectreAnalysisProgressReporter();
                reporter.Attach(ctx);
                var progress = new SynchronousProgress(reporter);
                action(reporter, progress);
                reporter.FinalizeSession();
            });

        reporter?.FlushPendingMessages();
    }
}
