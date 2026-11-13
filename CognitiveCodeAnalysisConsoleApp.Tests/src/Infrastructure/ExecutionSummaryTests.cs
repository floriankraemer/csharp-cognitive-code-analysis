/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Infrastructure;

public class ExecutionSummaryTests
{
    private sealed class Recorder : IProgress<AnalysisProgress>
    {
        public List<AnalysisProgress> Reports { get; } = [];

        public void Report(AnalysisProgress value) => Reports.Add(value);
    }

    private static string Write(ExecutionSummary summary)
    {
        using var writer = new StringWriter { NewLine = "\n" };
        summary.WriteTo(writer);
        return writer.ToString();
    }

    [Test]
    public void WriteTo_PrintsTotalPhasesAndStatistics()
    {
        // The clock moves 100 ms per read: start=100, reports at 200, 300, 400, 500, 600, summary at 700.
        var summary = new ExecutionSummary(new SteppingTimeProvider());
        var progress = summary.Observe(new Recorder());

        progress.Report(new AnalysisProgress(AnalysisProgressPhase.SearchingFiles));
        progress.Report(new AnalysisProgress(AnalysisProgressPhase.SearchCompleted, TotalFiles: 3));
        progress.Report(new AnalysisProgress(AnalysisProgressPhase.AnalysingFiles, TotalFiles: 3));
        progress.Report(new AnalysisProgress(AnalysisProgressPhase.AnalysingFiles, TotalFiles: 3, ProcessedFiles: 2));
        progress.Report(new AnalysisProgress(AnalysisProgressPhase.AnalysisCompleted, TotalFiles: 3, ProcessedFiles: 3));
        summary.FilesFound = 3;
        summary.MethodsAnalysed = 7;

        Assert.That(Write(summary), Is.EqualTo(
            """
            Execution Summary:
              Total execution time: 0.600s
            Phase timings:
                SearchingFiles: 0.100s
                AnalysingFiles: 0.200s
            Statistics:
                Files found: 3
                Files analysed: 3
                Files failed: 0
                Methods analysed: 7

            """.ReplaceLineEndings("\n")));
    }

    [Test]
    public void WriteTo_FilesThatWereFoundButNotAnalysed_AreCountedAsFailed()
    {
        var summary = new ExecutionSummary(new SteppingTimeProvider());
        summary.Observe(new Recorder()).Report(new AnalysisProgress(AnalysisProgressPhase.AnalysingFiles, TotalFiles: 5, ProcessedFiles: 3));
        summary.FilesFound = 5;

        string text = Write(summary);

        Assert.That(text, Does.Contain("Files analysed: 3"));
        Assert.That(text, Does.Contain("Files failed: 2"));
    }

    [Test]
    public void WriteTo_NoReports_PrintsEmptyPhaseList()
    {
        string text = Write(new ExecutionSummary(new SteppingTimeProvider()));

        Assert.That(text, Does.Contain("Phase timings:\nStatistics:"));
        Assert.That(text, Does.Contain("Files found: 0"));
        Assert.That(text, Does.Contain("Files failed: 0"));
    }

    [Test]
    public void WriteTo_ListsPhasesInTheOrderTheyStarted_AndFoldsCompletionIntoThem()
    {
        var summary = new ExecutionSummary(new SteppingTimeProvider());
        var progress = summary.Observe(new Recorder());
        foreach (var phase in new[]
        {
            AnalysisProgressPhase.CompilingSources, AnalysisProgressPhase.CompilationCompleted,
            AnalysisProgressPhase.AnalysingCoupling, AnalysisProgressPhase.CouplingCompleted,
            AnalysisProgressPhase.CalculatingScores, AnalysisProgressPhase.ScoresCalculated,
            AnalysisProgressPhase.ApplyingCoverage, AnalysisProgressPhase.CoverageApplied,
            AnalysisProgressPhase.ComparingBaseline, AnalysisProgressPhase.BaselineCompared,
            AnalysisProgressPhase.WritingReport, AnalysisProgressPhase.ReportCompleted,
        })
        {
            progress.Report(new AnalysisProgress(phase));
        }

        var names = Write(summary).Split('\n').Where(l => l.StartsWith("    ") && l.Contains("s")).Select(l => l.Trim().Split(':')[0]);

        Assert.That(names.Take(6), Is.EqualTo(new[]
        {
            "CompilingSources", "AnalysingCoupling", "CalculatingScores", "ApplyingCoverage", "ComparingBaseline", "WritingReport",
        }));
    }

    [Test]
    public void Observe_ForwardsEveryReportToTheInnerProgress()
    {
        var inner = new Recorder();
        var report = new AnalysisProgress(AnalysisProgressPhase.SearchingFiles);

        new ExecutionSummary(new SteppingTimeProvider()).Observe(inner).Report(report);

        Assert.That(inner.Reports, Is.EqualTo(new[] { report }));
    }

    [Test]
    public void Observe_ReportsFromSeveralThreads_AreAllRecorded()
    {
        var summary = new ExecutionSummary(new SteppingTimeProvider());
        var progress = summary.Observe(new Recorder());

        Parallel.For(0, 200, i => progress.Report(new AnalysisProgress(AnalysisProgressPhase.AnalysingFiles, 200, i + 1)));

        Assert.That(Write(summary), Does.Contain("Files analysed: 200"));
    }
}
