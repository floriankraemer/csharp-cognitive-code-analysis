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
}
