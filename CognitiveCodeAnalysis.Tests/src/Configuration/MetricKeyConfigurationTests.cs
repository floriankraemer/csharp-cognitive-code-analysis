/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.Configuration;

public class MetricKeyConfigurationTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private CognitiveConfiguration LoadMetrics(string metricsJson)
        => ConfigurationLoader.Load(_temp.CreateFileWithContent("config.json", $$"""{ "cognitive": { "metrics": { {{metricsJson}} } } }"""));

    [Test]
    public void Load_UnknownMetricKey_ThrowsNamingTheKeyAndListingTheValidOnes()
    {
        var ex = Assert.Throws<ConfigurationException>(
            () => LoadMetrics("""  "ifCuont": { "threshold": 1, "scale": 1.0, "enabled": true } """));

        Assert.That(ex!.Message, Does.Contain("Unknown metric key 'ifCuont' in cognitive.metrics"));
        foreach (string valid in ScoreCalculator.SupportedMetricKeys)
        {
            Assert.That(ex.Message, Does.Contain(valid));
        }
    }

    [Test]
    public void Load_SeveralUnknownMetricKeys_AreAllNamed()
    {
        var ex = Assert.Throws<ConfigurationException>(
            () => LoadMetrics("""  "foo": { "enabled": true }, "bar": { "enabled": false } """));

        Assert.That(ex!.Message, Does.Contain("Unknown metric keys 'bar', 'foo'"));
    }

    [TestCase("methodName")]
    [TestCase("MethodName")]
    [TestCase("methodSignature")]
    [TestCase("cyclomaticComplexity")]
    public void Load_KeyOfANonScoredMetricsMember_IsRejectedInsteadOfCrashingTheScoring(string key)
    {
        var ex = Assert.Throws<ConfigurationException>(
            () => LoadMetrics($$"""  "{{key}}": { "threshold": 1, "scale": 1.0, "enabled": true } """));

        Assert.That(ex!.Message, Does.Contain($"'{key}'"));
    }

    [Test]
    public void Load_AllSupportedKeys_AreAccepted_WithoutWarnings()
    {
        string metrics = string.Join(", ", ScoreCalculator.SupportedMetricKeys.Select(
            key => $$""" "{{key}}": { "threshold": 1, "scale": 1.0, "enabled": true } """));

        var configuration = LoadMetrics(metrics);

        Assert.That(configuration.Warnings, Is.Empty);
        Assert.That(configuration.Metrics.Keys, Is.EquivalentTo(ScoreCalculator.SupportedMetricKeys));
    }

    [Test]
    public void Load_DeprecatedAliases_AreAcceptedWithAWarningAndTreatedAsTheCurrentKeys()
    {
        var configuration = LoadMetrics("""
            "variableCount": { "threshold": 2, "scale": 3.0, "enabled": true },
            "propertyCallCount": { "threshold": 5, "scale": 7.0, "enabled": true }
            """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(configuration.Metrics.ContainsKey("variableCount"), Is.False);
            Assert.That(configuration.Metrics.ContainsKey("propertyCallCount"), Is.False);
            Assert.That(configuration.Metrics["localVariableCount"].Enabled, Is.True);
            Assert.That(configuration.Metrics["localVariableCount"].Threshold, Is.EqualTo(2));
            Assert.That(configuration.Metrics["localVariableCount"].Scale, Is.EqualTo(3.0));
            Assert.That(configuration.Metrics["propertyAccessCount"].Enabled, Is.True);
            Assert.That(configuration.Metrics["propertyAccessCount"].Threshold, Is.EqualTo(5));
            Assert.That(configuration.Warnings, Is.EqualTo(new[]
            {
                "Metric key 'propertyCallCount' is deprecated, use 'propertyAccessCount'. It is treated as 'propertyAccessCount'.",
                "Metric key 'variableCount' is deprecated, use 'localVariableCount'. It is treated as 'localVariableCount'.",
            }));
        }
    }

    [Test]
    public void Load_DeprecatedAlias_ScoresLikeTheCurrentKey()
    {
        var configuration = LoadMetrics(""" "variableCount": { "threshold": 1, "scale": 1.0, "enabled": true } """);
        var metrics = new CognitiveMetrics("M", "C", "f.cs", "M()", 1, localVariableCount: 5);

        new ScoreCalculator().CalculateScores(metrics, configuration);

        Assert.That(metrics.localVariableScore, Is.EqualTo(Math.Log(1 + 4.0)).Within(1e-12));
    }

    [Test]
    public void Load_DeprecatedAliasNextToAnUnknownKey_StillRejectsTheUnknownKey()
    {
        var ex = Assert.Throws<ConfigurationException>(
            () => LoadMetrics(""" "variableCount": { "enabled": true }, "nope": { "enabled": true } """));

        Assert.That(ex!.Message, Does.Contain("'nope'").And.Not.Contain("'variableCount'"));
    }

    [Test]
    public void Load_FileWithoutMetricsSection_KeepsDefaultsWithoutWarnings()
    {
        var configuration = ConfigurationLoader.Load(_temp.CreateFileWithContent("c.json", """{ "cognitive": { "scoreThreshold": 1 } }"""));

        Assert.That(configuration.Warnings, Is.Empty);
        Assert.That(configuration.Metrics.Keys, Is.EquivalentTo(ScoreCalculator.SupportedMetricKeys));
    }
}
