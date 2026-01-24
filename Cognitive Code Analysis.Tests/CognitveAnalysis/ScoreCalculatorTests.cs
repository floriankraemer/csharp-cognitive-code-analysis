using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class ScoreCalculatorTests
{
    [Fact]
    public void CalculateScores_WithValidMetric()
    {
        CognitiveMetrics metrics = new CognitiveMetrics(
            methodName: "TestMethod",
            className: "TestClass",
            filePath: "TestFile.cs",
            methodSignature: "TestMethod()",
            methodLineNumber: 10,
            ifCount: 10,
            elseCount: 10,
            argumentCount: 5,
            returnCount: 6
        );

        CognitiveConfiguration configuration = getConfiguration();
        ScoreCalculator calculator = new(configuration);

        calculator.CalculateScores(metrics);

        // Assert count values
        Assert.Equal(10, metrics.ifCount);
        Assert.Equal(10, metrics.elseCount);
        Assert.Equal(5, metrics.argumentCount);
        Assert.Equal(6, metrics.returnCount);

        // Assert score values
        // ifCount: 10, Threshold: 3, Scale: 1.0 -> Math.Log(1 + (10 - 3) / 1.0) = Math.Log(8) ≈ 2.07944
        Assert.Equal(2.07944, metrics.ifScore, 5);

        // elseCount: 10, Threshold: 1, Scale: 1.0 -> Math.Log(1 + (10 - 1) / 1.0) = Math.Log(10) ≈ 2.30259
        Assert.Equal(2.30259, metrics.elseScore, 5);

        // argumentCount: 5, Threshold: 4, Scale: 1.0 -> Math.Log(1 + (5 - 4) / 1.0) = Math.Log(2) ≈ 0.69315
        Assert.Equal(0.69315, metrics.argumentScore, 5);

        // returnCount: 6, Threshold: 2, Scale: 5.0 -> Math.Log(1 + (6 - 2) / 5.0) = Math.Log(1.8) ≈ 0.58779
        Assert.Equal(0.58779, metrics.returnScore, 5);
    }

    private static CognitiveConfiguration getConfiguration()
    {
        CognitiveConfiguration configuration = new();
        configuration.Metrics = new Dictionary<string, MetricConfiguration>
        {
            { "linesOfCode", new MetricConfiguration { Scale = 25.0, Threshold = 60, Enabled = true } },
            { "argumentCount", new MetricConfiguration { Scale = 1.0, Threshold = 4, Enabled = true } },
            { "returnCount", new MetricConfiguration { Scale = 5.0, Threshold = 2, Enabled = true } },
            { "variableCount", new MetricConfiguration { Scale = 5.0, Threshold = 4, Enabled = true } },
            { "propertyCallCount", new MetricConfiguration { Scale = 15.0, Threshold = 4, Enabled = true } },
            { "ifCount", new MetricConfiguration { Scale = 1.0, Threshold = 3, Enabled = true } },
            { "nestingLevels", new MetricConfiguration { Scale = 1.0, Threshold = 1, Enabled = true } },
            { "elseCount", new MetricConfiguration { Scale = 1.0, Threshold = 1, Enabled = true } }
        };
        return configuration;
    }
}
