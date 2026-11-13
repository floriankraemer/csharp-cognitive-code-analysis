/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Churn;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.Reporting.Spi;

namespace CognitiveCodeAnalysis.Reporting;

/// <summary>Copies the internal result types into the immutable records of the plugin interface.</summary>
internal static class CustomReportInputFactory
{
    internal static CognitiveReportInput Cognitive(
        CognitiveMetricsCollection metrics,
        CognitiveConfiguration configuration,
        CognitiveBaselineComparison? baseline) =>
        new()
        {
            Methods = ReportMetricsFilter.OrderRows(metrics, configuration).Select(m => Row(m, baseline)).ToList().AsReadOnly(),
            HasBaseline = baseline is not null,
            Settings = new CognitiveReportSettings
            {
                ScoreThreshold = configuration.ScoreThreshold,
                ShowOnlyMethodsExceedingThreshold = configuration.ShowOnlyMethodsExceedingThreshold,
                GroupByClass = configuration.GroupByClass,
                ShowHalsteadComplexity = configuration.ShowHalsteadComplexity,
                ShowCyclomaticComplexity = configuration.ShowCyclomaticComplexity,
                ShowCouplingMetrics = configuration.ShowCouplingMetrics,
                ShowDetailedCognitiveMetrics = configuration.ShowDetailedCognitiveMetrics,
            },
        };

    internal static ChurnReportInput Churn(IReadOnlyList<ChurnMetrics> churn, bool coverageProvided, DateOnly since) =>
        new()
        {
            CoverageProvided = coverageProvided,
            Since = since,
            Classes = churn.Select(c => new ChurnRow
            {
                ClassName = c.ClassName,
                FilePath = CognitiveCiLocation.DisplayPath(c.File),
                Score = c.Score,
                TimesChanged = c.TimesChanged,
                Churn = c.Churn,
                Coverage = c.Coverage,
                RiskChurn = c.RiskChurn,
                RiskLevel = c.RiskLevel?.ToString().ToUpperInvariant(),
            }).ToList().AsReadOnly(),
        };

    private static CountedMetric Counted(double count, double score) => new() { Count = count, Score = score };

    private static CognitiveMethodRow Row(CognitiveMetrics m, CognitiveBaselineComparison? baseline)
    {
        MethodMetricsComparison? comparison = null;
        baseline?.TryGetMethodComparison(m, out comparison);

        return new CognitiveMethodRow
        {
            FilePath = CognitiveCiLocation.DisplayPath(m.FilePath),
            ClassName = m.ClassName,
            MethodName = m.MethodName,
            MethodSignature = m.methodSignature,
            Line = m.methodLineNumber,
            TotalScore = m.totalScore,
            LinesOfCode = Counted(m.linesOfCode, m.linesOfCodeScore),
            Arguments = Counted(m.argumentCount, m.argumentScore),
            Returns = Counted(m.returnCount, m.returnScore),
            LocalVariables = Counted(m.localVariableCount, m.localVariableScore),
            PropertyAccesses = Counted(m.propertyAccessCount, m.propertyAccessScore),
            FieldAccesses = Counted(m.fieldAccessCount, m.fieldAccessScore),
            Ifs = Counted(m.ifCount, m.ifScore),
            Elses = Counted(m.elseCount, m.elseScore),
            Nesting = Counted(m.nestingLevels, m.nestingScore),
            Loops = Counted(m.loopCount, m.loopScore),
            Switches = Counted(m.switchCount, m.switchScore),
            TryCatches = Counted(m.tryCatchCount, m.tryCatchScore),
            CyclomaticComplexity = m.cyclomaticComplexity,
            HalsteadVolume = m.Halstead?.Volume,
            HalsteadDifficulty = m.Halstead?.Difficulty,
            HalsteadEffort = m.Halstead?.Effort,
            LineCoverage = m.lineCoveragePercentage,
            BranchCoverage = m.branchCoveragePercentage,
            Deltas = comparison is { HasBaseline: true }
                ? new CognitiveMethodDeltas
                {
                    TotalScore = comparison.TotalScore.Delta,
                    CyclomaticComplexity = comparison.CyclomaticComplexity.Delta,
                    LineCoverage = comparison.LineCoveragePercentage.Delta,
                    BranchCoverage = comparison.BranchCoveragePercentage.Delta,
                }
                : null,
        };
    }
}
