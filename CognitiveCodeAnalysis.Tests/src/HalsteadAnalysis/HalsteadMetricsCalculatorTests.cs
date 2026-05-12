/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.HalsteadAnalysis;

namespace CognitiveCodeAnalysis.Tests.HalsteadAnalysis;

public class HalsteadMetricsCalculatorTests
{
    [Test]
    public void Calculate_MatchesPhpStyleExample_TwoDistinctOpsTwoDistinctOperands()
    {
        var operators = new[] { "OpA", "OpA", "OpB" };
        var operands = new[] { "x", "x", "y" };

        HalsteadMetrics m = HalsteadMetricsCalculator.Calculate(operators, operands, "Test::m");

        Assert.That(m.DistinctOperators, Is.EqualTo(2));
        Assert.That(m.DistinctOperands, Is.EqualTo(2));
        Assert.That(m.TotalOperators, Is.EqualTo(3));
        Assert.That(m.TotalOperands, Is.EqualTo(3));
        Assert.That(m.ProgramLength, Is.EqualTo(6));
        Assert.That(m.ProgramVocabulary, Is.EqualTo(4));
        Assert.That(m.Volume, Is.EqualTo(6.0 * Math.Log(4, 2)).Within(1e-9));
        Assert.That(m.Difficulty, Is.EqualTo((2.0 / 2.0) * (3.0 / 2.0)).Within(1e-9));
        Assert.That(m.Effort, Is.EqualTo(m.Difficulty * m.Volume).Within(1e-9));
        Assert.That(m.Identifier, Is.EqualTo("Test::m"));
    }

    [Test]
    public void Calculate_EmptyLists_ZeroVolumeAndDifficulty()
    {
        HalsteadMetrics m = HalsteadMetricsCalculator.Calculate([], [], "empty");

        Assert.That(m.ProgramVocabulary, Is.EqualTo(0));
        Assert.That(m.Volume, Is.EqualTo(0));
        Assert.That(m.Difficulty, Is.EqualTo(0));
        Assert.That(m.Effort, Is.EqualTo(0));
    }

    [Test]
    public void Calculate_NoOperands_DifficultyZero()
    {
        HalsteadMetrics m = HalsteadMetricsCalculator.Calculate(["A"], [], "y");

        Assert.That(m.DistinctOperands, Is.EqualTo(0));
        Assert.That(m.Difficulty, Is.EqualTo(0));
    }
}
