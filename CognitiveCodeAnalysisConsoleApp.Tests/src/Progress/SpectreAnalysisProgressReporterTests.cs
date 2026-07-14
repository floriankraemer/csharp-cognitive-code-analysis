/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysisConsoleApp.Progress;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Progress;

public class SpectreAnalysisProgressReporterTests
{
    [Test]
    public void ApplyProgress_SearchCompleted_SetsFoundFileCountAndMessage()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(AnalysisProgressPhase.SearchCompleted, TotalFiles: 5));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.State.SearchCompleted, Is.True);
            Assert.That(reporter.State.FoundFileCount, Is.EqualTo(5));
            Assert.That(reporter.State.SearchCompleteMessage, Is.EqualTo("Found 5 C# file(s)"));
        }
    }

    [Test]
    public void ApplyProgress_AnalysingFiles_SetsDescriptionWithCounts()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.AnalysingFiles,
            TotalFiles: 5,
            ProcessedFiles: 2
        ));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.State.AnalysisDescription, Is.EqualTo("Analysing files (2/5)"));
            Assert.That(reporter.State.AnalysisValue, Is.EqualTo(2));
            Assert.That(reporter.State.AnalysisMaxValue, Is.EqualTo(5));
        }
    }

    [Test]
    public void Report_ConcurrentAnalysingFiles_DoesNotThrow()
    {
        var reporter = new SpectreAnalysisProgressReporter();
        const int totalFiles = 20;

        Parallel.For(1, totalFiles + 1, processed =>
        {
            reporter.Report(new AnalysisProgress(
                AnalysisProgressPhase.AnalysingFiles,
                TotalFiles: totalFiles,
                ProcessedFiles: processed
            ));
        });

        Assert.That(reporter.State.AnalysisMaxValue, Is.EqualTo(totalFiles));
    }

    [Test]
    public void ApplyProgress_SearchingFiles_SetsSearchStarted()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(AnalysisProgressPhase.SearchingFiles));

        Assert.That(reporter.State.SearchStarted, Is.True);
    }

    [Test]
    public void ApplyProgress_AnalysisCompleted_SetsCompletedFlag()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.AnalysisCompleted,
            TotalFiles: 3,
            ProcessedFiles: 3
        ));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.State.AnalysisCompleted, Is.True);
            Assert.That(reporter.State.AnalysisValue, Is.EqualTo(3));
            Assert.That(reporter.State.AnalysisMaxValue, Is.EqualTo(3));
        }
    }

    [Test]
    public void ApplyProgress_WritingReport_SetsDescriptionWithReportNameAndCounts()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.WritingReport,
            TotalFiles: 5,
            ProcessedFiles: 2,
            ReportName: "Html"
        ));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.State.ReportDescription, Is.EqualTo("Writing Html report (2/5)"));
            Assert.That(reporter.State.ReportValue, Is.EqualTo(2));
            Assert.That(reporter.State.ReportMaxValue, Is.EqualTo(5));
        }
    }

    [Test]
    public void ApplyProgress_AnalysingFiles_IgnoresOutOfOrderUpdates()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        Assert.That(reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.AnalysingFiles,
            TotalFiles: 10,
            ProcessedFiles: 5
        )), Is.True);
        Assert.That(reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.AnalysingFiles,
            TotalFiles: 10,
            ProcessedFiles: 3
        )), Is.False);

        Assert.That(reporter.State.AnalysisValue, Is.EqualTo(5));
    }

    [Test]
    public void ApplyProgress_AnalysingCoupling_SetsDescriptionWithCounts()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.AnalysingCoupling,
            TotalFiles: 8,
            ProcessedFiles: 3
        ));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.State.CouplingDescription, Is.EqualTo("Analysing coupling (3/8)"));
            Assert.That(reporter.State.CouplingValue, Is.EqualTo(3));
            Assert.That(reporter.State.CouplingMaxValue, Is.EqualTo(8));
        }
    }

    [Test]
    public void ApplyProgress_CouplingCompleted_SetsCompletedFlag()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.CouplingCompleted,
            TotalFiles: 8,
            ProcessedFiles: 8
        ));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.State.CouplingCompleted, Is.True);
            Assert.That(reporter.State.CouplingValue, Is.EqualTo(8));
            Assert.That(reporter.State.CouplingMaxValue, Is.EqualTo(8));
        }
    }

    [Test]
    public void ApplyProgress_ReportCompleted_SetsCompletedFlag()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.ReportCompleted,
            TotalFiles: 3,
            ProcessedFiles: 3,
            ReportName: "Csv"
        ));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.State.ReportCompleted, Is.True);
            Assert.That(reporter.State.ReportValue, Is.EqualTo(3));
            Assert.That(reporter.State.ReportMaxValue, Is.EqualTo(3));
        }
    }
}
