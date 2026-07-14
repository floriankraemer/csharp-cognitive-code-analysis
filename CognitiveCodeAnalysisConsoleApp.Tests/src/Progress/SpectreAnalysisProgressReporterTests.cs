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
    public void ApplyProgress_CompilingSources_SetsDescriptionWithCounts()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.CompilingSources,
            TotalFiles: 12,
            ProcessedFiles: 4
        ));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.State.CompileDescription, Is.EqualTo("Compiling sources (4/12)"));
            Assert.That(reporter.State.CompileValue, Is.EqualTo(4));
            Assert.That(reporter.State.CompileMaxValue, Is.EqualTo(12));
        }
    }

    [Test]
    public void ApplyProgress_CalculatingScores_SetsDescriptionWithCounts()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.CalculatingScores,
            TotalFiles: 500,
            ProcessedFiles: 200
        ));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.State.ScoresDescription, Is.EqualTo("Calculating scores (200/500)"));
            Assert.That(reporter.State.ScoresValue, Is.EqualTo(200));
            Assert.That(reporter.State.ScoresMaxValue, Is.EqualTo(500));
        }
    }

    [Test]
    public void ApplyProgress_ApplyingCoverage_SetsDescriptionWithCounts()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.ApplyingCoverage,
            TotalFiles: 30,
            ProcessedFiles: 10
        ));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.State.CoverageDescription, Is.EqualTo("Applying coverage (10/30)"));
            Assert.That(reporter.State.CoverageValue, Is.EqualTo(10));
            Assert.That(reporter.State.CoverageMaxValue, Is.EqualTo(30));
        }
    }

    [Test]
    public void ApplyProgress_ComparingBaseline_SetsDescriptionWithCounts()
    {
        var reporter = new SpectreAnalysisProgressReporter();

        reporter.ApplyProgress(new AnalysisProgress(
            AnalysisProgressPhase.ComparingBaseline,
            TotalFiles: 30,
            ProcessedFiles: 10
        ));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(reporter.State.BaselineDescription, Is.EqualTo("Comparing baseline (10/30)"));
            Assert.That(reporter.State.BaselineValue, Is.EqualTo(10));
            Assert.That(reporter.State.BaselineMaxValue, Is.EqualTo(30));
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
