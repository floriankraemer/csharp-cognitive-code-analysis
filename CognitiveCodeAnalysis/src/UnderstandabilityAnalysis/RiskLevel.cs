/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.UnderstandabilityAnalysis;

/// <summary>
/// The risk bands of a complexity value, as used for the cyclomatic complexity: low up to 5, medium up to 10,
/// high up to 15, very_high above.
/// </summary>
public static class RiskLevel
{
    private const int LowMax = 5;
    private const int MediumMax = 10;
    private const int HighMax = 15;

    public static string Of(int complexity)
    {
        if (complexity <= LowMax)
        {
            return "low";
        }

        if (complexity <= MediumMax)
        {
            return "medium";
        }

        return complexity <= HighMax ? "high" : "very_high";
    }
}
