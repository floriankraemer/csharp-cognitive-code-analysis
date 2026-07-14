/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysisConsoleApp.Progress;

public sealed class SpectreAnalysisProgressState
{
    public bool SearchStarted { get; internal set; }
    public bool SearchCompleted { get; internal set; }
    public int FoundFileCount { get; internal set; }
    public string? SearchCompleteMessage { get; internal set; }
    public string? CompileDescription { get; internal set; }
    public double CompileValue { get; internal set; }
    public double CompileMaxValue { get; internal set; }
    public bool CompileCompleted { get; internal set; }
    public string? AnalysisDescription { get; internal set; }
    public double AnalysisValue { get; internal set; }
    public double AnalysisMaxValue { get; internal set; }
    public bool AnalysisCompleted { get; internal set; }
    public string? CouplingDescription { get; internal set; }
    public double CouplingValue { get; internal set; }
    public double CouplingMaxValue { get; internal set; }
    public bool CouplingCompleted { get; internal set; }
    public string? ScoresDescription { get; internal set; }
    public double ScoresValue { get; internal set; }
    public double ScoresMaxValue { get; internal set; }
    public bool ScoresCompleted { get; internal set; }
    public string? CoverageDescription { get; internal set; }
    public double CoverageValue { get; internal set; }
    public double CoverageMaxValue { get; internal set; }
    public bool CoverageCompleted { get; internal set; }
    public string? BaselineDescription { get; internal set; }
    public double BaselineValue { get; internal set; }
    public double BaselineMaxValue { get; internal set; }
    public bool BaselineCompleted { get; internal set; }
    public string? ReportDescription { get; internal set; }
    public double ReportValue { get; internal set; }
    public double ReportMaxValue { get; internal set; }
    public bool ReportCompleted { get; internal set; }
    public string? ReportGeneratedMessage { get; internal set; }
}
