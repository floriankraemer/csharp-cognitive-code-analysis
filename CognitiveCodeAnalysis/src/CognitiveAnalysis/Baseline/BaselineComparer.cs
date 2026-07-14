/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

public static class BaselineComparer
{
    public static CognitiveBaselineComparison Compare(
        CognitiveMetricsCollection current,
        CognitiveBaselineSnapshot baseline
    ) => Compare(current, baseline, progress: null);

    public static CognitiveBaselineComparison Compare(
        CognitiveMetricsCollection current,
        CognitiveBaselineSnapshot baseline,
        IProgress<AnalysisProgress>? progress
    )
    {
        var baselineMethods = ToDictionaryLastWins(
            baseline.Methods,
            BaselineMethodKey.FromSnapshot);

        int totalMethods = current.Count;
        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.ComparingBaseline,
            TotalFiles: totalMethods,
            ProcessedFiles: 0
        ));

        var methodsByKey = new Dictionary<string, MethodMetricsComparison>(StringComparer.Ordinal);
        const int progressBatchSize = 100;
        int processedMethods = 0;

        foreach (CognitiveMetrics metrics in current)
        {
            var key = BaselineMethodKey.FromMetrics(metrics);
            baselineMethods.TryGetValue(key, out CognitiveBaselineMethodSnapshot? baselineMethod);
            methodsByKey[key] = BuildMethodComparison(metrics, baselineMethod);

            processedMethods++;
            if (processedMethods % progressBatchSize == 0 || processedMethods == totalMethods)
            {
                progress?.Report(new AnalysisProgress(
                    AnalysisProgressPhase.ComparingBaseline,
                    TotalFiles: totalMethods,
                    ProcessedFiles: processedMethods
                ));
            }
        }

        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.BaselineCompared,
            TotalFiles: totalMethods,
            ProcessedFiles: totalMethods
        ));

        var baselineCoupling = ToDictionaryLastWins(
            baseline.ClassCoupling,
            c => c.ClassName);

        var classCouplingByName = new Dictionary<string, ClassCouplingComparison>(StringComparer.Ordinal);
        foreach (var classGroup in current.GroupBy(m => m.ClassName, StringComparer.Ordinal))
        {
            if (!current.TryGetClassCoupling(classGroup.Key, out ClassCouplingMetrics? currentCoupling)
                || currentCoupling == null)
            {
                continue;
            }

            baselineCoupling.TryGetValue(classGroup.Key, out CognitiveBaselineClassCouplingSnapshot? baselineCouplingSnapshot);
            classCouplingByName[classGroup.Key] = BuildClassCouplingComparison(currentCoupling, baselineCouplingSnapshot);
        }

        return new CognitiveBaselineComparison(methodsByKey, classCouplingByName);
    }

    private static MethodMetricsComparison BuildMethodComparison(
        CognitiveMetrics current,
        CognitiveBaselineMethodSnapshot? baseline
    )
    {
        if (baseline == null)
        {
            return new MethodMetricsComparison
            {
                Current = current,
                TotalScore = MetricDelta.FromCurrentOnly(current.totalScore),
                LinesOfCode = MetricDelta.FromCurrentOnly(current.linesOfCode),
                LinesOfCodeScore = MetricDelta.FromCurrentOnly(current.linesOfCodeScore),
                IfCount = MetricDelta.FromCurrentOnly(current.ifCount),
                IfScore = MetricDelta.FromCurrentOnly(current.ifScore),
                ElseCount = MetricDelta.FromCurrentOnly(current.elseCount),
                ElseScore = MetricDelta.FromCurrentOnly(current.elseScore),
                LoopCount = MetricDelta.FromCurrentOnly(current.loopCount),
                LoopScore = MetricDelta.FromCurrentOnly(current.loopScore),
                SwitchCount = MetricDelta.FromCurrentOnly(current.switchCount),
                SwitchScore = MetricDelta.FromCurrentOnly(current.switchScore),
                TryCatchCount = MetricDelta.FromCurrentOnly(current.tryCatchCount),
                TryCatchScore = MetricDelta.FromCurrentOnly(current.tryCatchScore),
                ReturnCount = MetricDelta.FromCurrentOnly(current.returnCount),
                ReturnScore = MetricDelta.FromCurrentOnly(current.returnScore),
                ArgumentCount = MetricDelta.FromCurrentOnly(current.argumentCount),
                ArgumentScore = MetricDelta.FromCurrentOnly(current.argumentScore),
                NestingLevels = MetricDelta.FromCurrentOnly(current.nestingLevels),
                NestingScore = MetricDelta.FromCurrentOnly(current.nestingScore),
                LocalVariableCount = MetricDelta.FromCurrentOnly(current.localVariableCount),
                LocalVariableScore = MetricDelta.FromCurrentOnly(current.localVariableScore),
                FieldAccessCount = MetricDelta.FromCurrentOnly(current.fieldAccessCount),
                FieldAccessScore = MetricDelta.FromCurrentOnly(current.fieldAccessScore),
                PropertyAccessCount = MetricDelta.FromCurrentOnly(current.propertyAccessCount),
                PropertyAccessScore = MetricDelta.FromCurrentOnly(current.propertyAccessScore),
                CyclomaticComplexity = MetricDelta.FromCurrentOnly(current.cyclomaticComplexity),
                HalsteadVolume = MetricDelta.FromCurrentOnly(current.Halstead?.Volume ?? 0),
                HalsteadDifficulty = MetricDelta.FromCurrentOnly(current.Halstead?.Difficulty ?? 0),
                HalsteadEffort = MetricDelta.FromCurrentOnly(current.Halstead?.Effort ?? 0),
                LineCoveragePercentage = MetricDelta.FromOptional(current.lineCoveragePercentage, null),
                BranchCoveragePercentage = MetricDelta.FromOptional(current.branchCoveragePercentage, null),
                ChurnScore = MetricDelta.FromOptional(current.churnScore, null),
            };
        }

        return new MethodMetricsComparison
        {
            Current = current,
            Baseline = baseline,
            TotalScore = MetricDelta.FromRequired(current.totalScore, baseline.TotalScore),
            LinesOfCode = MetricDelta.FromRequired(current.linesOfCode, baseline.LinesOfCode),
            LinesOfCodeScore = MetricDelta.FromRequired(current.linesOfCodeScore, baseline.LinesOfCodeScore),
            IfCount = MetricDelta.FromRequired(current.ifCount, baseline.IfCount),
            IfScore = MetricDelta.FromRequired(current.ifScore, baseline.IfScore),
            ElseCount = MetricDelta.FromRequired(current.elseCount, baseline.ElseCount),
            ElseScore = MetricDelta.FromRequired(current.elseScore, baseline.ElseScore),
            LoopCount = MetricDelta.FromRequired(current.loopCount, baseline.LoopCount),
            LoopScore = MetricDelta.FromRequired(current.loopScore, baseline.LoopScore),
            SwitchCount = MetricDelta.FromRequired(current.switchCount, baseline.SwitchCount),
            SwitchScore = MetricDelta.FromRequired(current.switchScore, baseline.SwitchScore),
            TryCatchCount = MetricDelta.FromRequired(current.tryCatchCount, baseline.TryCatchCount),
            TryCatchScore = MetricDelta.FromRequired(current.tryCatchScore, baseline.TryCatchScore),
            ReturnCount = MetricDelta.FromRequired(current.returnCount, baseline.ReturnCount),
            ReturnScore = MetricDelta.FromRequired(current.returnScore, baseline.ReturnScore),
            ArgumentCount = MetricDelta.FromRequired(current.argumentCount, baseline.ArgumentCount),
            ArgumentScore = MetricDelta.FromRequired(current.argumentScore, baseline.ArgumentScore),
            NestingLevels = MetricDelta.FromRequired(current.nestingLevels, baseline.NestingLevels),
            NestingScore = MetricDelta.FromRequired(current.nestingScore, baseline.NestingScore),
            LocalVariableCount = MetricDelta.FromRequired(current.localVariableCount, baseline.LocalVariableCount),
            LocalVariableScore = MetricDelta.FromRequired(current.localVariableScore, baseline.LocalVariableScore),
            FieldAccessCount = MetricDelta.FromRequired(current.fieldAccessCount, baseline.FieldAccessCount),
            FieldAccessScore = MetricDelta.FromRequired(current.fieldAccessScore, baseline.FieldAccessScore),
            PropertyAccessCount = MetricDelta.FromRequired(current.propertyAccessCount, baseline.PropertyAccessCount),
            PropertyAccessScore = MetricDelta.FromRequired(current.propertyAccessScore, baseline.PropertyAccessScore),
            CyclomaticComplexity = MetricDelta.FromRequired(current.cyclomaticComplexity, baseline.CyclomaticComplexity),
            HalsteadVolume = MetricDelta.FromOptional(current.Halstead?.Volume, baseline.HalsteadVolume),
            HalsteadDifficulty = MetricDelta.FromOptional(current.Halstead?.Difficulty, baseline.HalsteadDifficulty),
            HalsteadEffort = MetricDelta.FromOptional(current.Halstead?.Effort, baseline.HalsteadEffort),
            LineCoveragePercentage = MetricDelta.FromOptional(current.lineCoveragePercentage, baseline.LineCoveragePercentage),
            BranchCoveragePercentage = MetricDelta.FromOptional(current.branchCoveragePercentage, baseline.BranchCoveragePercentage),
            ChurnScore = MetricDelta.FromOptional(current.churnScore, baseline.ChurnScore),
        };
    }

    private static ClassCouplingComparison BuildClassCouplingComparison(
        ClassCouplingMetrics current,
        CognitiveBaselineClassCouplingSnapshot? baseline
    )
    {
        if (baseline == null)
        {
            return new ClassCouplingComparison
            {
                Current = current,
                IncomingCoupling = MetricDelta.FromCurrentOnly(current.IncomingCoupling),
                OutgoingCoupling = MetricDelta.FromCurrentOnly(current.OutgoingCoupling),
                Stability = MetricDelta.FromCurrentOnly(current.Stability),
            };
        }

        return new ClassCouplingComparison
        {
            Current = current,
            Baseline = baseline,
            IncomingCoupling = MetricDelta.FromRequired(current.IncomingCoupling, baseline.IncomingCoupling),
            OutgoingCoupling = MetricDelta.FromRequired(current.OutgoingCoupling, baseline.OutgoingCoupling),
            Stability = MetricDelta.FromRequired(current.Stability, baseline.Stability),
        };
    }

    private static Dictionary<string, T> ToDictionaryLastWins<T>(
        IEnumerable<T> items,
        Func<T, string> keySelector
    )
    {
        var dictionary = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (T item in items)
        {
            dictionary[keySelector(item)] = item;
        }

        return dictionary;
    }
}
