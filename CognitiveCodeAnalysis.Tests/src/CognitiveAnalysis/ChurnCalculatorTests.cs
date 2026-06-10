/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class ChurnCalculatorTests
{
    [Test]
    public void CalculateChurnScore_WithoutCoverage_AssumesZeroCoverage()
    {
        var metrics = SampleMetrics(totalScore: 10.0);

        double churn = ChurnCalculator.CalculateChurnScore(metrics);

        Assert.That(churn, Is.EqualTo(10.0).Within(0.0001));
    }

    [Test]
    public void CalculateChurnScore_UsesLineCoverageWhenBranchMissing()
    {
        var metrics = SampleMetrics(totalScore: 8.0, lineCoveragePercentage: 50.0);

        double churn = ChurnCalculator.CalculateChurnScore(metrics);

        Assert.That(churn, Is.EqualTo(4.0).Within(0.0001));
    }

    [Test]
    public void CalculateChurnScore_PrefersBranchCoverageOverLineCoverage()
    {
        var metrics = SampleMetrics(
            totalScore: 10.0,
            lineCoveragePercentage: 20.0,
            branchCoveragePercentage: 80.0
        );

        double churn = ChurnCalculator.CalculateChurnScore(metrics);

        Assert.That(churn, Is.EqualTo(2.0).Within(0.0001));
    }

    [Test]
    public void CalculateChurnScore_FullCoverage_YieldsZeroRisk()
    {
        var metrics = SampleMetrics(
            totalScore: 5.0,
            branchCoveragePercentage: 100.0
        );

        double churn = ChurnCalculator.CalculateChurnScore(metrics);

        Assert.That(churn, Is.EqualTo(0.0).Within(0.0001));
    }

    private static CognitiveMetrics SampleMetrics(
        double totalScore,
        double? lineCoveragePercentage = null,
        double? branchCoveragePercentage = null
    )
    {
        var metrics = new CognitiveMetrics(
            methodName: "Risky",
            className: "Demo",
            filePath: "src/Demo.cs",
            methodSignature: "void Risky()",
            methodLineNumber: 1,
            lineCoveragePercentage: lineCoveragePercentage,
            branchCoveragePercentage: branchCoveragePercentage
        );
        metrics.totalScore = totalScore;
        return metrics;
    }
}
