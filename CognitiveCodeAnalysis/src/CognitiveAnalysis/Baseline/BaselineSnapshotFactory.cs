/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

public static class BaselineSnapshotFactory
{
    public static CognitiveBaselineSnapshot FromMetricsCollection(CognitiveMetricsCollection metricsCollection) =>
        FromMetricsCollection(metricsCollection, relativePaths: false, configHash: null, DateTimeOffset.UtcNow);

    /// <summary>
    /// A snapshot for a baseline file. With <paramref name="relativePaths"/> the paths are relative to the working
    /// directory, so the file also matches in another checkout.
    /// </summary>
    public static CognitiveBaselineSnapshot FromMetricsCollection(
        CognitiveMetricsCollection metricsCollection,
        bool relativePaths,
        string? configHash,
        DateTimeOffset generatedAt)
    {
        var methods = metricsCollection
            .Select(m => ToMethodSnapshot(m, relativePaths))
            .OrderBy(m => m.FilePath, StringComparer.Ordinal)
            .ThenBy(m => m.ClassName, StringComparer.Ordinal)
            .ThenBy(m => m.MethodSignature, StringComparer.Ordinal)
            .ToList();

        var classCoupling = new List<CognitiveBaselineClassCouplingSnapshot>();
        foreach (var classGroup in metricsCollection.GroupBy(m => m.ClassName, StringComparer.Ordinal))
        {
            if (!metricsCollection.TryGetClassCoupling(classGroup.Key, out var coupling) || coupling == null)
            {
                continue;
            }

            classCoupling.Add(new CognitiveBaselineClassCouplingSnapshot
            {
                ClassName = coupling.ClassName,
                IncomingCoupling = coupling.IncomingCoupling,
                OutgoingCoupling = coupling.OutgoingCoupling,
                Stability = coupling.Stability,
            });
        }

        classCoupling.Sort((a, b) => string.Compare(a.ClassName, b.ClassName, StringComparison.Ordinal));

        return new CognitiveBaselineSnapshot
        {
            SchemaVersion = CognitiveBaselineSnapshot.CurrentSchemaVersion,
            GeneratedAt = generatedAt,
            ConfigHash = configHash,
            Methods = methods,
            ClassCoupling = classCoupling,
        };
    }

    public static CognitiveBaselineMethodSnapshot FromMetrics(CognitiveMetrics metrics) =>
        ToMethodSnapshot(metrics, relativePaths: false);

    private static CognitiveBaselineMethodSnapshot ToMethodSnapshot(CognitiveMetrics metrics, bool relativePaths) =>
        new()
        {
            FilePath = relativePaths ? CognitiveCiLocation.DisplayPath(metrics.FilePath) : metrics.FilePath,
            ClassName = metrics.ClassName,
            MethodName = metrics.MethodName,
            MethodSignature = metrics.methodSignature,
            MethodLineNumber = metrics.methodLineNumber,
            TotalScore = metrics.totalScore,
            LinesOfCode = metrics.linesOfCode,
            LinesOfCodeScore = metrics.linesOfCodeScore,
            IfCount = metrics.ifCount,
            IfScore = metrics.ifScore,
            ElseCount = metrics.elseCount,
            ElseScore = metrics.elseScore,
            LoopCount = metrics.loopCount,
            LoopScore = metrics.loopScore,
            SwitchCount = metrics.switchCount,
            SwitchScore = metrics.switchScore,
            TryCatchCount = metrics.tryCatchCount,
            TryCatchScore = metrics.tryCatchScore,
            ReturnCount = metrics.returnCount,
            ReturnScore = metrics.returnScore,
            ArgumentCount = metrics.argumentCount,
            ArgumentScore = metrics.argumentScore,
            NestingLevels = metrics.nestingLevels,
            NestingScore = metrics.nestingScore,
            LocalVariableCount = metrics.localVariableCount,
            LocalVariableScore = metrics.localVariableScore,
            FieldAccessCount = metrics.fieldAccessCount,
            FieldAccessScore = metrics.fieldAccessScore,
            PropertyAccessCount = metrics.propertyAccessCount,
            PropertyAccessScore = metrics.propertyAccessScore,
            CyclomaticComplexity = metrics.cyclomaticComplexity,
            HalsteadVolume = metrics.Halstead?.Volume,
            HalsteadDifficulty = metrics.Halstead?.Difficulty,
            HalsteadEffort = metrics.Halstead?.Effort,
            LineCoveragePercentage = metrics.lineCoveragePercentage,
            BranchCoveragePercentage = metrics.branchCoveragePercentage,
            ChurnScore = metrics.churnScore,
            Understandability = metrics.Understandability.Total,
            UnderstandabilityRiskLevel = metrics.Understandability.RiskLevel,
        };
}
