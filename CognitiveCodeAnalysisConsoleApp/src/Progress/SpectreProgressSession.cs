/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.Progress;

internal static class SpectreProgressSession
{
    public static void Run(Action<SpectreAnalysisProgressReporter, IProgress<AnalysisProgress>> action)
    {
        if (!AnsiConsole.Profile.Capabilities.Interactive)
        {
            var silentReporter = new SpectreAnalysisProgressReporter();
            var silentProgress = new Progress<AnalysisProgress>(silentReporter.Report);
            action(silentReporter, silentProgress);
            silentReporter.FlushPendingMessages();
            return;
        }

        SpectreAnalysisProgressReporter? reporter = null;

        AnsiConsole.Progress()
            .AutoClear(true)
            .HideCompleted(true)
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
                var progress = new Progress<AnalysisProgress>(reporter.Report);
                action(reporter, progress);
            });

        reporter?.FlushPendingMessages();
    }
}
