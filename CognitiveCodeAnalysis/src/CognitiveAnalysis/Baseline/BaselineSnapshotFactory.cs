/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

public static class BaselineSnapshotFactory
{
    public static CognitiveBaselineSnapshot FromMetricsCollection(CognitiveMetricsCollection metricsCollection)
    {
        var methods = metricsCollection
            .Select(ToMethodSnapshot)
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
            GeneratedAt = DateTimeOffset.UtcNow,
            Methods = methods,
            ClassCoupling = classCoupling,
        };
    }

    public static CognitiveBaselineMethodSnapshot FromMetrics(CognitiveMetrics metrics) =>
        ToMethodSnapshot(metrics);

    private static CognitiveBaselineMethodSnapshot ToMethodSnapshot(CognitiveMetrics metrics) =>
        new()
        {
            FilePath = metrics.FilePath,
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
        };
}
