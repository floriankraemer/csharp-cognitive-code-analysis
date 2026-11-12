/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void CalculateScores_MapsLinesOfCodeToScoreAndTotalScore()
    {
        var metrics = new CognitiveMetrics(
            methodName: "TestMethod",
            className: "TestClass",
            filePath: "TestFile.cs",
            methodSignature: "TestMethod()",
            methodLineNumber: 10,
            linesOfCode: 85
        );

        var configuration = new CognitiveConfiguration
        {
            Metrics = new Dictionary<string, MetricConfiguration>
            {
                { "linesOfCode", new MetricConfiguration { Scale = 25.0, Threshold = 60, Enabled = true } },
            }
        };

        new ScoreCalculator().CalculateScores(metrics, configuration);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metrics.linesOfCodeScore, Is.EqualTo(Math.Log(2)));
            Assert.That(metrics.totalScore, Is.EqualTo(metrics.linesOfCodeScore));
        }
    }

    [Test]
    public void CalculateScores_MapsLocalVariableAndPropertyAccessCounts()
    {
        var metrics = new CognitiveMetrics(
            methodName: "TestMethod",
            className: "TestClass",
            filePath: "TestFile.cs",
            methodSignature: "TestMethod()",
            methodLineNumber: 10,
            localVariableCount: 5,
            propertyAccessCount: 6
        );

        var configuration = new CognitiveConfiguration
        {
            Metrics = new Dictionary<string, MetricConfiguration>
            {
                { "localVariableCount", new MetricConfiguration { Scale = 5.0, Threshold = 4, Enabled = true } },
                { "propertyAccessCount", new MetricConfiguration { Scale = 15.0, Threshold = 4, Enabled = true } },
            }
        };

        new ScoreCalculator().CalculateScores(metrics, configuration);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metrics.localVariableScore, Is.EqualTo(Math.Log(1.2)));
            Assert.That(metrics.propertyAccessScore, Is.EqualTo(Math.Log(1 + 2.0 / 15.0)));
            Assert.That(metrics.totalScore, Is.EqualTo(metrics.localVariableScore + metrics.propertyAccessScore));
        }
    }

    [Test]
    public void CalculateScores_DisabledMetricsDoNotContribute()
    {
        var metrics = new CognitiveMetrics(
            methodName: "TestMethod",
            className: "TestClass",
            filePath: "TestFile.cs",
            methodSignature: "TestMethod()",
            methodLineNumber: 10,
            localVariableCount: 10,
            fieldAccessCount: 10
        );

        var configuration = new CognitiveConfiguration
        {
            Metrics = new Dictionary<string, MetricConfiguration>
            {
                { "localVariableCount", new MetricConfiguration { Scale = 5.0, Threshold = 4, Enabled = false } },
                { "fieldAccessCount", new MetricConfiguration { Scale = 15.0, Threshold = 4, Enabled = false } },
            }
        };

        new ScoreCalculator().CalculateScores(metrics, configuration);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metrics.localVariableScore, Is.EqualTo(0));
            Assert.That(metrics.fieldAccessScore, Is.EqualTo(0));
            Assert.That(metrics.totalScore, Is.EqualTo(0));
        }
    }

    [Test]
    public void CalculateScoresWithValidMetric()
    {
        // Arrange
        var metrics = new CognitiveMetrics(
            methodName: "TestMethod" ,
            className: "TestClass" ,
            filePath: "TestFile.cs" ,
            methodSignature: "TestMethod()" ,
            methodLineNumber: 10 ,
            ifCount: 10 ,
            elseCount: 10 ,
            argumentCount: 5,
            returnCount: 6
        );

        var configuration = GetConfiguration();
        var calculator = new ScoreCalculator();

        // Act
        calculator.CalculateScores(metrics, configuration);

        // Assert
        using (Assert.EnterMultipleScope())
        {
            // Assert count values
            Assert.That(metrics.ifCount, Is.EqualTo(10));
            Assert.That(metrics.elseCount, Is.EqualTo(10));
            Assert.That(metrics.argumentCount, Is.EqualTo(5));
            Assert.That(metrics.returnCount, Is.EqualTo(6));

            // Assert score values
            Assert.That(metrics.ifScore, Is.EqualTo(2.0794415416798357d));
            Assert.That(metrics.elseScore, Is.EqualTo(2.3025850929940459d));
            Assert.That(metrics.argumentScore, Is.EqualTo(0.69314718055994529d));
            Assert.That(metrics.returnScore, Is.EqualTo(0.58778666490211906d));
        }
    }

    [Test]
    public void CalculateScores_MapsAllRemainingKnownCountMetrics()
    {
        var metrics = new CognitiveMetrics(
            methodName: "TestMethod",
            className: "TestClass",
            filePath: "TestFile.cs",
            methodSignature: "TestMethod()",
            methodLineNumber: 1,
            loopCount: 5,
            switchCount: 3,
            tryCatchCount: 2,
            nestingLevels: 4,
            fieldAccessCount: 6
        );

        var configuration = new CognitiveConfiguration
        {
            Metrics = new Dictionary<string, MetricConfiguration>
            {
                { "loopCount", new MetricConfiguration { Scale = 1.0, Threshold = 2, Enabled = true } },
                { "switchCount", new MetricConfiguration { Scale = 1.0, Threshold = 1, Enabled = true } },
                { "tryCatchCount", new MetricConfiguration { Scale = 1.0, Threshold = 1, Enabled = true } },
                { "nestingLevels", new MetricConfiguration { Scale = 1.0, Threshold = 2, Enabled = true } },
                { "fieldAccessCount", new MetricConfiguration { Scale = 5.0, Threshold = 2, Enabled = true } },
            }
        };

        new ScoreCalculator().CalculateScores(metrics, configuration);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(metrics.loopScore, Is.EqualTo(Math.Log(1 + (5.0 - 2.0) / 1.0)));
            Assert.That(metrics.switchScore, Is.EqualTo(Math.Log(1 + (3.0 - 1.0) / 1.0)));
            Assert.That(metrics.tryCatchScore, Is.EqualTo(Math.Log(1 + (2.0 - 1.0) / 1.0)));
            Assert.That(metrics.nestingScore, Is.EqualTo(Math.Log(1 + (4.0 - 2.0) / 1.0)));
            Assert.That(metrics.fieldAccessScore, Is.EqualTo(Math.Log(1 + (6.0 - 2.0) / 5.0)));
        }
    }

    private static CognitiveConfiguration GetConfiguration()
    {
        CognitiveConfiguration configuration = new()
        {
            Metrics = new Dictionary<string , MetricConfiguration>
            {
                { "linesOfCode", new MetricConfiguration { Scale = 25.0, Threshold = 60, Enabled = true } },
                { "argumentCount", new MetricConfiguration { Scale = 1.0, Threshold = 4, Enabled = true } },
                { "returnCount", new MetricConfiguration { Scale = 5.0, Threshold = 2, Enabled = true } },
                { "localVariableCount", new MetricConfiguration { Scale = 5.0, Threshold = 4, Enabled = true } },
                { "propertyAccessCount", new MetricConfiguration { Scale = 15.0, Threshold = 4, Enabled = true } },
                { "ifCount", new MetricConfiguration { Scale = 1.0, Threshold = 3, Enabled = true } },
                { "nestingLevels", new MetricConfiguration { Scale = 1.0, Threshold = 1, Enabled = true } },
                { "elseCount", new MetricConfiguration { Scale = 1.0, Threshold = 1, Enabled = true } }
            }
        };

        return configuration;
    }

    private static CognitiveConfiguration SingleMetric(string key, int threshold, double scale, bool enabled = true) => new()
    {
        Metrics = new Dictionary<string, MetricConfiguration>
        {
            { key, new MetricConfiguration { Scale = scale, Threshold = threshold, Enabled = enabled } },
        }
    };

    private static CognitiveMetrics Metrics(
        int ifCount = 0, int elseCount = 0, int loopCount = 0, int switchCount = 0, int tryCatchCount = 0,
        int returnCount = 0, int argumentCount = 0, int linesOfCode = 0, int nestingLevels = 0,
        int localVariableCount = 0, int fieldAccessCount = 0, int propertyAccessCount = 0) =>
        new("M", "C", "f.cs", "M()", 1, ifCount, elseCount, loopCount, switchCount, tryCatchCount, returnCount,
            argumentCount, linesOfCode, nestingLevels, 0, localVariableCount, fieldAccessCount, propertyAccessCount);

    [Test]
    public void CalculateScores_DisabledMetric_IsNotScored()
    {
        var metrics = Metrics(ifCount: 10);

        new ScoreCalculator().CalculateScores(metrics, SingleMetric("ifCount", 1, 1, enabled: false));

        Assert.That(metrics.ifScore, Is.EqualTo(0.0));
        Assert.That(metrics.totalScore, Is.EqualTo(0.0));
    }

    [TestCase(2)]
    [TestCase(3)]
    public void CalculateScores_CountAtOrBelowThreshold_ScoresZero(int count)
    {
        var metrics = Metrics(ifCount: count);

        new ScoreCalculator().CalculateScores(metrics, SingleMetric("ifCount", 3, 1));

        Assert.That(metrics.ifScore, Is.EqualTo(0.0));
    }

    [Test]
    public void CalculateScores_CountJustAboveThreshold_ScoresLogOfOnePlusExcessOverScale()
    {
        var metrics = Metrics(ifCount: 4);

        new ScoreCalculator().CalculateScores(metrics, SingleMetric("ifCount", 3, 2));

        Assert.That(metrics.ifScore, Is.EqualTo(Math.Log(1.5)).Within(1e-12));
    }

    [Test]
    public void CalculateScores_EachKnownMetric_WritesOnlyItsOwnScore()
    {
        (string Key, Func<CognitiveMetrics> Make, Func<CognitiveMetrics, double> Score)[] cases =
        [
            ("ifCount", () => Metrics(ifCount: 5), m => m.ifScore),
            ("elseCount", () => Metrics(elseCount: 5), m => m.elseScore),
            ("loopCount", () => Metrics(loopCount: 5), m => m.loopScore),
            ("switchCount", () => Metrics(switchCount: 5), m => m.switchScore),
            ("tryCatchCount", () => Metrics(tryCatchCount: 5), m => m.tryCatchScore),
            ("returnCount", () => Metrics(returnCount: 5), m => m.returnScore),
            ("argumentCount", () => Metrics(argumentCount: 5), m => m.argumentScore),
            ("nestingLevels", () => Metrics(nestingLevels: 5), m => m.nestingScore),
            ("linesOfCode", () => Metrics(linesOfCode: 5), m => m.linesOfCodeScore),
            ("localVariableCount", () => Metrics(localVariableCount: 5), m => m.localVariableScore),
            ("fieldAccessCount", () => Metrics(fieldAccessCount: 5), m => m.fieldAccessScore),
            ("propertyAccessCount", () => Metrics(propertyAccessCount: 5), m => m.propertyAccessScore),
        ];

        foreach (var (key, make, score) in cases)
        {
            var metrics = make();
            new ScoreCalculator().CalculateScores(metrics, SingleMetric(key, 1, 1));

            double expected = Math.Log(1 + 4.0);
            Assert.That(score(metrics), Is.EqualTo(expected).Within(1e-12), key);
            Assert.That(metrics.totalScore, Is.EqualTo(expected).Within(1e-12), key + " total");
        }
    }

    [Test]
    public void CalculateScores_TotalScore_SumsAllTwelveScores()
    {
        var metrics = Metrics(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var configuration = new CognitiveConfiguration
        {
            Metrics = new[]
            {
                "ifCount", "elseCount", "loopCount", "switchCount", "tryCatchCount", "returnCount", "argumentCount",
                "linesOfCode", "nestingLevels", "localVariableCount", "fieldAccessCount", "propertyAccessCount",
            }.ToDictionary(k => k, _ => new MetricConfiguration { Threshold = 0, Scale = 1, Enabled = true })
        };

        new ScoreCalculator().CalculateScores(metrics, configuration);

        Assert.That(metrics.totalScore, Is.EqualTo(12 * Math.Log(2)).Within(1e-12));
    }

    [Test]
    public void CalculateScores_UnknownMetricName_IsIgnored()
    {
        var metrics = Metrics(ifCount: 9);

        new ScoreCalculator().CalculateScores(metrics, SingleMetric("doesNotExist", 0, 1));

        Assert.That(metrics.totalScore, Is.EqualTo(0.0));
    }

    [Test]
    public void CalculateScores_PublicFieldWithoutScoreField_IsReadViaReflectionButNotStored()
    {
        // methodLineNumber is a public int field: readable by name, but has no matching score slot.
        var metrics = Metrics();
        metrics.methodLineNumber = 50;

        new ScoreCalculator().CalculateScores(metrics, SingleMetric("methodLineNumber", 10, 1));

        Assert.That(metrics.totalScore, Is.EqualTo(0.0));
    }

    [Test]
    public void CalculateScores_CyclomaticComplexity_IsReadButContributesNoScore()
    {
        var metrics = Metrics();
        metrics.cyclomaticComplexity = 20;

        new ScoreCalculator().CalculateScores(metrics, SingleMetric("cyclomaticComplexity", 5, 1));

        Assert.That(metrics.totalScore, Is.EqualTo(0.0));
    }

    [Test]
    public void CalculateScores_EmptyMetricConfiguration_LeavesMetricsUntouched()
    {
        var metrics = Metrics(ifCount: 9);

        var result = new ScoreCalculator().CalculateScores(metrics, new CognitiveConfiguration { Metrics = new() });

        Assert.That(result, Is.SameAs(metrics));
        Assert.That(result.totalScore, Is.EqualTo(0.0));
    }
}
