/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.HalsteadAnalysis;

/// <summary>
/// Halstead metric formulas aligned with <c>HalsteadMetricsCalculator.php</c> in cognitive-code-checker.
/// </summary>
public static class HalsteadMetricsCalculator
{
    public static HalsteadMetrics Calculate(
        IReadOnlyList<string> operators,
        IReadOnlyList<string> operands,
        string identifier
    ) {
        int distinctOperators = operators.Distinct().Count();
        int distinctOperands = operands.Distinct().Count();
        int totalOperators = operators.Count;
        int totalOperands = operands.Count;

        int programLength = totalOperators + totalOperands;
        int programVocabulary = distinctOperators + distinctOperands;

        double volume = programVocabulary <= 0
            ? 0.0
            : programLength * Math.Log(programVocabulary, 2);

        double difficulty = distinctOperands == 0
            ? 0.0
            : (distinctOperators / 2.0) * (totalOperands / (double)distinctOperands);

        double effort = difficulty * volume;

        return new HalsteadMetrics
        {
            DistinctOperators = distinctOperators,
            DistinctOperands = distinctOperands,
            TotalOperators = totalOperators,
            TotalOperands = totalOperands,
            ProgramLength = programLength,
            ProgramVocabulary = programVocabulary,
            Volume = volume,
            Difficulty = difficulty,
            Effort = effort,
            Identifier = identifier,
        };
    }
}
