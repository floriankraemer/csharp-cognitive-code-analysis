/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public enum AnalysisProgressPhase
{
    SearchingFiles,
    SearchCompleted,
    CompilingSources,
    CompilationCompleted,
    AnalysingFiles,
    AnalysisCompleted,
    AnalysingCoupling,
    CouplingCompleted,
    CalculatingScores,
    ScoresCalculated,
    ApplyingCoverage,
    CoverageApplied,
    ComparingBaseline,
    BaselineCompared,
    WritingReport,
    ReportCompleted
}

public readonly record struct AnalysisProgress(
    AnalysisProgressPhase Phase,
    int TotalFiles = 0,
    int ProcessedFiles = 0,
    string? ReportName = null
);
