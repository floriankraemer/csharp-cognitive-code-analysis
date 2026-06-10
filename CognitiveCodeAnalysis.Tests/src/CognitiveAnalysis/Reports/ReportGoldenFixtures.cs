/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;
using CognitiveCodeAnalysis.HalsteadAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

/// <summary>
/// Canonical metrics and configuration for golden report tests. Uses fixed file paths for stable CI fingerprints.
/// </summary>
internal static class ReportGoldenFixtures
{
    internal const double ScoreThreshold = 5.0;

    internal static CognitiveConfiguration StandardConfig() =>
        new()
        {
            ScoreThreshold = ScoreThreshold,
            ShowOnlyMethodsExceedingThreshold = false,
            GroupByClass = false,
        };

    internal static CognitiveConfiguration GroupedConfig() =>
        new()
        {
            ScoreThreshold = ScoreThreshold,
            ShowOnlyMethodsExceedingThreshold = false,
            GroupByClass = true,
            ShowCouplingMetrics = true,
        };

    internal static CognitiveMetricsCollection StandardCollection()
    {
        var high = CreateMetric(
            methodName: "Alpha",
            className: "Demo",
            filePath: "src/Demo.cs",
            methodSignature: "void Alpha()",
            methodLineNumber: 10,
            totalScore: 8.5
        );
        high.ifCount = 2;
        high.ifScore = 0.4;
        high.elseCount = 1;
        high.elseScore = 0.2;
        high.loopCount = 1;
        high.loopScore = 0.15;
        high.switchCount = 0;
        high.switchScore = 0.0;
        high.tryCatchCount = 1;
        high.tryCatchScore = 0.1;
        high.argumentCount = 3;
        high.argumentScore = 0.3;
        high.nestingLevels = 2;
        high.nestingScore = 0.25;
        high.returnCount = 1;
        high.returnScore = 0.1;
        high.localVariableCount = 4;
        high.localVariableScore = 0.2;
        high.fieldAccessCount = 2;
        high.fieldAccessScore = 0.1;
        high.propertyAccessCount = 1;
        high.propertyAccessScore = 0.05;
        high.linesOfCode = 25;
        high.linesOfCodeScore = 0.5;

        var low = CreateMetric(
            methodName: "Beta",
            className: "Demo",
            filePath: "src/Demo.cs",
            methodSignature: "void Beta()",
            methodLineNumber: 42,
            totalScore: 1.2
        );
        low.ifCount = 0;
        low.ifScore = 0.0;
        low.elseCount = 0;
        low.elseScore = 0.0;
        low.loopCount = 0;
        low.loopScore = 0.0;
        low.switchCount = 1;
        low.switchScore = 0.1;
        low.tryCatchCount = 0;
        low.tryCatchScore = 0.0;
        low.argumentCount = 1;
        low.argumentScore = 0.05;
        low.nestingLevels = 0;
        low.nestingScore = 0.0;
        low.returnCount = 0;
        low.returnScore = 0.0;
        low.localVariableCount = 1;
        low.localVariableScore = 0.05;
        low.fieldAccessCount = 0;
        low.fieldAccessScore = 0.0;
        low.propertyAccessCount = 2;
        low.propertyAccessScore = 0.1;
        low.linesOfCode = 8;
        low.linesOfCodeScore = 0.1;

        return new CognitiveMetricsCollection { high, low };
    }

    internal static CognitiveMetricsCollection GroupedCollection()
    {
        var coll = StandardCollection();
        coll.SetClassCouplingMetrics(
        [
            new ClassCouplingMetrics
            {
                ClassName = "Demo",
                IncomingCoupling = 2,
                OutgoingCoupling = 1,
                Stability = 1.0 / 3.0,
            },
        ]);
        return coll;
    }

    private static CognitiveMetrics CreateMetric(
        string methodName,
        string className,
        string filePath,
        string methodSignature,
        int methodLineNumber,
        double totalScore
    )
    {
        var m = new CognitiveMetrics(
            methodName: methodName,
            className: className,
            filePath: filePath,
            methodSignature: methodSignature,
            methodLineNumber: methodLineNumber,
            ifCount: 0,
            elseCount: 0,
            loopCount: 0,
            switchCount: 0,
            tryCatchCount: 0,
            returnCount: 0,
            argumentCount: 0,
            linesOfCode: 1,
            nestingLevels: 0,
            cyclomaticComplexity: 1,
            localVariableCount: 0,
            fieldAccessCount: 0,
            propertyAccessCount: 0
        );
        m.totalScore = totalScore;
        return m;
    }
}
