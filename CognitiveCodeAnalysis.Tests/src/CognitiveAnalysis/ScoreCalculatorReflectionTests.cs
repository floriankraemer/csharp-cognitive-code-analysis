/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

/// <summary>
/// Metrics unknown to the built-in switch are resolved by naming convention through reflection:
/// count property/field by the configured name, score slot by replacing "Count"/"Levels" with "Score".
/// </summary>
public class ScoreCalculatorReflectionTests
{
    private sealed class ExtendedMetrics() : CognitiveMetrics("M", "C", "f.cs", "M()", 1)
    {
        public int WidgetCount { get; set; }

        public double WidgetScore { get; set; }

        public int gadgetCount;

        public double gadgetScore = 0;

        public int WrongTypeCount { get; set; }

        public string WrongTypeScore { get; set; } = "untouched";

        public int fieldWrongTypeCount;

        public string fieldWrongTypeScore = "untouched";

        public int? nullableCount;

        public double? nullableScore = null;

        public int x;

        public double X = 0;

        public int deepLevels;

        public double deepScore = 0;
    }

    private static CognitiveConfiguration Config(string key, int threshold = 1, double scale = 1) => new()
    {
        Metrics = new Dictionary<string, MetricConfiguration>
        {
            [key] = new() { Threshold = threshold, Scale = scale, Enabled = true },
        },
    };

    [Test]
    public void PascalCaseProperty_IsReadAndItsScorePropertyWritten()
    {
        var m = new ExtendedMetrics { WidgetCount = 5 };

        new ScoreCalculator().CalculateScores(m, Config("widgetCount"));

        Assert.That(m.WidgetScore, Is.EqualTo(Math.Log(1 + 4.0)).Within(1e-12));
    }

    [Test]
    public void PublicField_IsReadAndItsScoreFieldWritten()
    {
        var m = new ExtendedMetrics { gadgetCount = 3 };

        new ScoreCalculator().CalculateScores(m, Config("gadgetCount", threshold: 1, scale: 2));

        Assert.That(m.gadgetScore, Is.EqualTo(Math.Log(1 + 2.0 / 2.0)).Within(1e-12));
    }

    [Test]
    public void LevelsSuffix_MapsToScoreSlot()
    {
        var m = new ExtendedMetrics { deepLevels = 4 };

        new ScoreCalculator().CalculateScores(m, Config("deepLevels"));

        Assert.That(m.deepScore, Is.EqualTo(Math.Log(4.0)).Within(1e-12));
    }

    [Test]
    public void BelowThreshold_WritesZeroOverPreviousValue()
    {
        var m = new ExtendedMetrics { WidgetCount = 1, WidgetScore = 9 };

        new ScoreCalculator().CalculateScores(m, Config("widgetCount"));

        Assert.That(m.WidgetScore, Is.EqualTo(0.0));
    }

    [Test]
    public void ScoreSlotOfWrongType_IsLeftUntouched()
    {
        var m = new ExtendedMetrics { WrongTypeCount = 9, fieldWrongTypeCount = 9 };

        new ScoreCalculator().CalculateScores(m, Config("wrongTypeCount"));
        new ScoreCalculator().CalculateScores(m, Config("fieldWrongTypeCount"));

        Assert.That(m.WrongTypeScore, Is.EqualTo("untouched"));
        Assert.That(m.fieldWrongTypeScore, Is.EqualTo("untouched"));
    }

    [Test]
    public void NullableCount_NullReadsAsZero_AndNullableScoreSlotIsNotDouble()
    {
        var m = new ExtendedMetrics { nullableCount = null };

        new ScoreCalculator().CalculateScores(m, Config("nullableCount", threshold: -5));

        // count reads as 0.0 > -5, so a score is computed but the slot is double? and therefore skipped.
        Assert.That(m.nullableScore, Is.Null);
    }

    [Test]
    public void SingleLetterMetricName_ResolvesCountButItsScoreSlotIsTheIntField()
    {
        var m = new ExtendedMetrics { x = 3 };

        // The score slot name equals the count field name ("x", an int), so nothing can be written.
        new ScoreCalculator().CalculateScores(m, Config("x", threshold: 0));

        Assert.That(m.x, Is.EqualTo(3));
        Assert.That(m.X, Is.EqualTo(0.0));
    }

    [Test]
    public void EmptyMetricName_IsIgnored()
    {
        var m = new ExtendedMetrics();

        Assert.DoesNotThrow(() => new ScoreCalculator().CalculateScores(m, Config("", threshold: -1)));
        Assert.That(m.totalScore, Is.EqualTo(0.0));
    }

    [Test]
    public void ReflectionScores_DoNotContributeToTotalScore()
    {
        var m = new ExtendedMetrics { WidgetCount = 50 };

        new ScoreCalculator().CalculateScores(m, Config("widgetCount"));

        Assert.That(m.WidgetScore, Is.GreaterThan(0));
        Assert.That(m.totalScore, Is.EqualTo(0.0));
    }

    [Test]
    public void KnownMetricOnSubclass_UsesBuiltInMapping()
    {
        var m = new ExtendedMetrics { ifCount = 4 };

        new ScoreCalculator().CalculateScores(m, Config("ifCount"));

        Assert.That(m.ifScore, Is.EqualTo(Math.Log(4.0)).Within(1e-12));
        Assert.That(m.totalScore, Is.EqualTo(m.ifScore));
    }
}
