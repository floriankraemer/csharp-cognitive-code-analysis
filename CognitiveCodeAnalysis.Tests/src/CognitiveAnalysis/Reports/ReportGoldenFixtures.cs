/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;
using CognitiveCodeAnalysis.HalsteadAnalysis;
using CognitiveCodeAnalysis.UnderstandabilityAnalysis;

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


    /// <summary>All optional columns on: Halstead, cyclomatic, coupling, coverage, churn and understandability.</summary>
    internal static CognitiveConfiguration FullConfig(bool groupByClass) =>
        new()
        {
            ScoreThreshold = ScoreThreshold,
            ShowOnlyMethodsExceedingThreshold = false,
            GroupByClass = groupByClass,
            ShowCouplingMetrics = true,
            ShowHalsteadComplexity = true,
            ShowCyclomaticComplexity = true,
            ShowUnderstandability = true,
        };

    /// <summary>
    /// Four methods in two classes covering every colour band, missing optional values and special characters.
    /// </summary>
    internal static CognitiveMetricsCollection FullCollection(
        double alphaScore = 8.5,
        double betaScore = 0.6,
        double gammaScore = 0.2
    )
    {
        var alpha = CreateMetric("Alpha", "Svc", "src/Svc.cs", "void Alpha(int a, string b)", 10, alphaScore);
        alpha.ifCount = 4;
        alpha.ifScore = 1.5;
        alpha.linesOfCode = 90;
        alpha.linesOfCodeScore = 0.25;
        alpha.cyclomaticComplexity = 7;
        alpha.Halstead = new HalsteadMetrics { Volume = 120.456, Difficulty = 14.5, Effort = 1746.6 };
        alpha.lineCoveragePercentage = 80.5;
        alpha.branchCoveragePercentage = 60;
        alpha.churnScore = 0.9;
        alpha.Understandability = new Understandability(Structural: 5, Hybrid: 2, Fundamental: 3, Nesting: 6, Recursion: 1);

        var beta = CreateMetric("Beta", "Svc", "src/Svc.cs", "List<string> Beta<T>()", 42, betaScore);
        beta.loopCount = 2;
        beta.loopScore = 0.3;
        beta.cyclomaticComplexity = 2;
        beta.lineCoveragePercentage = 100;
        beta.churnScore = 0.5;
        beta.Understandability = new Understandability(Structural: 3, Hybrid: 1, Fundamental: 1, Nesting: 2, Recursion: 0);

        var gamma = CreateMetric("Gamma", "Repo", "src/Repo.cs", "void Gamma()", 5, gammaScore);
        gamma.churnScore = 0.1;
        gamma.branchCoveragePercentage = 12.25;

        var delta = CreateMetric("Delta", "Repo", "src/Repo.cs", "void Delta()", 30, 0.45);

        var collection = new CognitiveMetricsCollection { alpha, beta, gamma, delta };
        collection.SetClassCouplingMetrics(
        [
            new ClassCouplingMetrics { ClassName = "Svc", IncomingCoupling = 3, OutgoingCoupling = 5, Stability = 0.625 },
            new ClassCouplingMetrics { ClassName = "Repo", IncomingCoupling = 1, OutgoingCoupling = 0, Stability = 0.0 },
        ]);
        return collection;
    }

    /// <summary>
    /// Baseline in which Alpha was cheaper (delta up), Beta was dearer (delta down), Gamma is unchanged and Delta is new.
    /// </summary>
    internal static CognitiveBaselineComparison FullBaselineComparison()
    {
        var baseline = FullCollection(alphaScore: 7.0, betaScore: 1.1, gammaScore: 0.2);
        var baselineMetrics = baseline.Where(m => m.MethodName != "Delta").ToList();
        var baselineCollection = new CognitiveMetricsCollection();
        foreach (var m in baselineMetrics)
        {
            baselineCollection.Add(m);
        }

        baselineCollection[0].ifCount = 1;
        baselineCollection[0].churnScore = 0.4;
        baselineCollection[0].Halstead = new HalsteadMetrics { Volume = 100.0, Difficulty = 16.5, Effort = 1746.6 };
        baselineCollection[1].lineCoveragePercentage = 90;
        baselineCollection.SetClassCouplingMetrics(
        [
            new ClassCouplingMetrics { ClassName = "Svc", IncomingCoupling = 1, OutgoingCoupling = 5, Stability = 0.8 },
        ]);

        var snapshot = BaselineSnapshotFactory.FromMetricsCollection(baselineCollection);
        return BaselineComparer.Compare(FullCollection(), snapshot);
    }

    /// <summary>
    /// Violations at warning and error severity, a method exactly at the threshold (not a violation), an error exactly
    /// at twice the threshold, and a method on line 0 that the formats report on line 1.
    /// </summary>
    internal static CognitiveMetricsCollection CiViolationsCollection() =>
        new()
        {
            CreateMetric("Warn", "Ci", "src/Ci.cs", "void Warn()", 4, 6.0),
            CreateMetric("Fail", "Ci", "src/Ci.cs", "void Fail()", 20, 10.0),
            CreateMetric("Edge", "Ci", "src/Ci.cs", "void Edge()", 30, 5.0),
            CreateMetric("Unlined", "Ci", "src/Lines.cs", "void Unlined()", 0, 7.4),
        };

    /// <summary>Every method at or below the threshold.</summary>
    internal static CognitiveMetricsCollection NoViolationsCollection() =>
        new()
        {
            CreateMetric("Quiet", "Ci", "src/Ci.cs", "void Quiet()", 4, 1.2),
            CreateMetric("Calm", "Ci", "src/Ci.cs", "void Calm()", 9, 5.0),
        };

    /// <summary>File names and class names with markup, quotes and the separators of the workflow commands.</summary>
    internal static CognitiveMetricsCollection EscapingCollection() =>
        new()
        {
            CreateMetric("Run", "Ops&Co<T>", "src/a & \"b\" <c>.cs", "void Run()", 3, 12.0),
            CreateMetric("Go", "Ops%Co", "src/x:y,z 100%.cs", "void Go()", 9, 6.0),
        };

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
