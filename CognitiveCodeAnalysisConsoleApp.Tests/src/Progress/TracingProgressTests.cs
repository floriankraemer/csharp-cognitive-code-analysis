/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;
using CognitiveCodeAnalysisConsoleApp.Progress;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Progress;

public class TracingProgressTests
{
    [Test]
    public void Report_LogsPhaseTransitionsOnly()
    {
        var inner = new RecordingProgress();
        var tracer = new RecordingTracer();
        var tracing = new TracingProgress(inner, tracer);

        tracing.Report(new AnalysisProgress(AnalysisProgressPhase.CompilingSources, TotalFiles: 3, ProcessedFiles: 0));
        tracing.Report(new AnalysisProgress(AnalysisProgressPhase.CompilingSources, TotalFiles: 3, ProcessedFiles: 1));
        tracing.Report(new AnalysisProgress(AnalysisProgressPhase.CompilationCompleted, TotalFiles: 3, ProcessedFiles: 3));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(inner.Reports, Has.Count.EqualTo(3));
            Assert.That(tracer.Messages, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(tracer.Messages[0], Does.Contain("CompilingSources"));
            Assert.That(tracer.Messages[^1], Does.Contain("CompilationCompleted"));
        }
    }

    private sealed class RecordingProgress : IProgress<AnalysisProgress>
    {
        public List<AnalysisProgress> Reports { get; } = [];

        public void Report(AnalysisProgress value) => Reports.Add(value);
    }

    private sealed class RecordingTracer : IAnalysisTracer
    {
        public List<string> Messages { get; } = [];

        public void Trace(string message) => Messages.Add(message);

        public void TraceStep(string stepName, Action action) => action();

        public T TraceStep<T>(string stepName, Func<T> action) => action();
    }
}
