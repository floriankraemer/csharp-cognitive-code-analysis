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
        ScoreCalculator calculator = new(configuration);

        // Act
        calculator.CalculateScores(metrics);

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

    private static CognitiveConfiguration GetConfiguration()
    {
        CognitiveConfiguration configuration = new()
        {
            Metrics = new Dictionary<string , MetricConfiguration>
            {
                { "linesOfCode", new MetricConfiguration { Scale = 25.0, Threshold = 60, Enabled = true } },
                { "argumentCount", new MetricConfiguration { Scale = 1.0, Threshold = 4, Enabled = true } },
                { "returnCount", new MetricConfiguration { Scale = 5.0, Threshold = 2, Enabled = true } },
                { "variableCount", new MetricConfiguration { Scale = 5.0, Threshold = 4, Enabled = true } },
                { "propertyCallCount", new MetricConfiguration { Scale = 15.0, Threshold = 4, Enabled = true } },
                { "ifCount", new MetricConfiguration { Scale = 1.0, Threshold = 3, Enabled = true } },
                { "nestingLevels", new MetricConfiguration { Scale = 1.0, Threshold = 1, Enabled = true } },
                { "elseCount", new MetricConfiguration { Scale = 1.0, Threshold = 1, Enabled = true } }
            }
        };

        return configuration;
    }
}
