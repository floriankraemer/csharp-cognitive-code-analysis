namespace CognitiveCodeAnalysis.CognitiveAnalysis;

/// <summary>
/// Calculates churn/risk score based on cognitive complexity and test coverage.
/// Higher score indicates higher risk (complex code with low test coverage).
/// </summary>
public static class ChurnCalculator
{
    /// <summary>
    /// Calculates churn/risk score based on cognitive complexity and test coverage.
    /// Formula: totalScore × (1 - CoverageFactor)
    /// Higher complexity and lower coverage result in higher churn scores.
    /// </summary>
    /// <param name="metrics">The cognitive metrics with coverage data</param>
    /// <returns>The churn score (higher = higher risk)</returns>
    public static double CalculateChurnScore(CognitiveMetrics metrics)
    {
        double coverageFactor = GetCoverageFactor(metrics);

        // Risk = Cognitive Score × (1 - Coverage Factor)
        // Higher cognitive score + lower coverage = higher risk
        return metrics.totalScore * (1.0 - coverageFactor);
    }

    /// <summary>
    /// Gets the coverage factor (0.0-1.0) from coverage percentages.
    /// Prefers branch coverage over line coverage as it's more comprehensive.
    /// </summary>
    /// <param name="metrics">The cognitive metrics with coverage data</param>
    /// <returns>Coverage factor between 0.0 (no coverage) and 1.0 (100% coverage)</returns>
    private static double GetCoverageFactor(CognitiveMetrics metrics)
    {
        // Prefer branch coverage if available (more comprehensive)
        if (metrics.branchCoveragePercentage.HasValue)
        {
            return metrics.branchCoveragePercentage.Value / 100.0;
        }

        // Fallback to line coverage if available
        if (metrics.lineCoveragePercentage.HasValue)
        {
            return metrics.lineCoveragePercentage.Value / 100.0;
        }

        // No coverage data = assume 0% coverage (highest risk)
        return 0.0;
    }
}
