using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.NUnitTests
{
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
            CognitiveMetrics metrics = new CognitiveMetrics(
                methodName: "TestMethod" ,
                className: "TestClass" ,
                filePath: "TestFile.cs" ,
                signature: "TestMethod()" ,
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

            // Assert count values
            Assert.That(metrics.ifCount , Is.EqualTo(10));
            Assert.That(metrics.elseCount , Is.EqualTo(10));
            Assert.That(metrics.argumentCount , Is.EqualTo(5));
            Assert.That(metrics.returnCount, Is.EqualTo(6));

            // Assert score values
            // ifCount: 10, Threshold: 3, Scale: 1.0 -> Math.Log(1 + (10 - 3) / 1.0) = Math.Log(8) ≈ 2.07944
            Assert.That(metrics.ifScore, Is.EqualTo(2.0794415416798357d));

            // elseCount: 10, Threshold: 1, Scale: 1.0 -> Math.Log(1 + (10 - 1) / 1.0) = Math.Log(10) ≈ 2.30259
            Assert.That(metrics.elseScore, Is.EqualTo(2.3025850929940459d));

            // argumentCount: 5, Threshold: 4, Scale: 1.0 -> Math.Log(1 + (5 - 4) / 1.0) = Math.Log(2) ≈ 0.69315
            Assert.That(metrics.argumentScore, Is.EqualTo(0.69314718055994529d));

            // returnCount: 6, Threshold: 2, Scale: 5.0 -> Math.Log(1 + (6 - 2) / 5.0) = Math.Log(1.8) ≈ 0.58779
            Assert.That(metrics.returnScore, Is.EqualTo(0.58778666490211906d));
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
}