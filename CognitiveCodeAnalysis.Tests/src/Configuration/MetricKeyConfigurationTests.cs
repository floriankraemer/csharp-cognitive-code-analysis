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

    private static string Unknown(string key)
        => $"Unknown metric '{key}' in configuration is ignored. Valid metrics: {string.Join(", ", ScoreCalculator.SupportedMetricKeys)}.";

    [Test]
    public void Load_UnknownMetricKey_IsIgnoredWithAWarningNamingTheKeyAndTheValidOnes()
    {
        var configuration = LoadMetrics("""  "ifCuont": { "threshold": 1, "scale": 1.0, "enabled": true } """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(configuration.Warnings, Is.EqualTo(new[] { Unknown("ifCuont") }));
            Assert.That(configuration.Metrics.ContainsKey("ifCuont"), Is.False);
            Assert.That(configuration.Metrics.Keys, Is.EquivalentTo(ScoreCalculator.SupportedMetricKeys));
        }
    }

    [Test]
    public void Load_SeveralUnknownMetricKeys_AreAllWarnedAbout()
    {
        var configuration = LoadMetrics("""  "foo": { "enabled": true }, "bar": { "enabled": false } """);

        Assert.That(configuration.Warnings, Is.EqualTo(new[] { Unknown("bar"), Unknown("foo") }));
    }

    [TestCase("methodName")]
    [TestCase("MethodName")]
    [TestCase("methodSignature")]
    [TestCase("cyclomaticComplexity")]
    public void Load_KeyOfANonScoredMetricsMember_IsIgnoredAndScoringStillWorks(string key)
    {
        var configuration = LoadMetrics($$"""  "{{key}}": { "threshold": 1, "scale": 1.0, "enabled": true } """);
        var metrics = new CognitiveMetrics("M", "C", "f.cs", "M()", 1, ifCount: 9);

        Assert.DoesNotThrow(() => new ScoreCalculator().CalculateScores(metrics, configuration));

        Assert.That(configuration.Warnings, Is.EqualTo(new[] { Unknown(key) }));
        Assert.That(metrics.ifScore, Is.GreaterThan(0));
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
    public void Load_AliasAndCurrentKeyBothConfigured_TheCurrentKeyWinsAndTheAliasIsWarnedAbout()
    {
        var configuration = LoadMetrics("""
            "variableCount": { "threshold": 9, "scale": 9.0, "enabled": true },
            "localVariableCount": { "threshold": 2, "scale": 3.0, "enabled": false }
            """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(configuration.Metrics.ContainsKey("variableCount"), Is.False);
            Assert.That(configuration.Metrics["localVariableCount"].Threshold, Is.EqualTo(2));
            Assert.That(configuration.Metrics["localVariableCount"].Enabled, Is.False);
            Assert.That(configuration.Warnings, Is.EqualTo(new[]
            {
                "Deprecated metric 'variableCount' ignored because 'localVariableCount' is also configured.",
            }));
        }
    }

    [Test]
    public void Load_BothAliasesAndBothCurrentKeys_CurrentKeysWin()
    {
        var configuration = LoadMetrics("""
            "variableCount": { "threshold": 9, "enabled": true }, "localVariableCount": { "threshold": 2, "enabled": true },
            "propertyCallCount": { "threshold": 8, "enabled": true }, "propertyAccessCount": { "threshold": 3, "enabled": true }
            """);

        Assert.That(configuration.Metrics["localVariableCount"].Threshold, Is.EqualTo(2));
        Assert.That(configuration.Metrics["propertyAccessCount"].Threshold, Is.EqualTo(3));
        Assert.That(configuration.Warnings, Has.Count.EqualTo(2).And.All.StartWith("Deprecated metric").And.All.Contain("is also configured"));
    }

    [Test]
    public void Load_AliasWithoutTheCurrentKeyInTheFile_StillWinsOverTheBuiltInDefault()
    {
        var configuration = LoadMetrics(""" "propertyCallCount": { "threshold": 8, "scale": 2.0, "enabled": true } """);

        Assert.That(configuration.Metrics["propertyAccessCount"].Threshold, Is.EqualTo(8));
        Assert.That(configuration.Metrics["propertyAccessCount"].Enabled, Is.True);
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
    public void Load_DeprecatedAliasNextToAnUnknownKey_WarnsAboutBoth()
    {
        var configuration = LoadMetrics(""" "variableCount": { "enabled": true }, "nope": { "enabled": true } """);

        Assert.That(configuration.Warnings, Has.Count.EqualTo(2));
        Assert.That(configuration.Warnings, Does.Contain(Unknown("nope")));
        Assert.That(configuration.Warnings.Single(w => w != Unknown("nope")), Does.Contain("'variableCount' is deprecated"));
    }

    [Test]
    public void Load_FileWithoutMetricsSection_KeepsDefaultsWithoutWarnings()
    {
        var configuration = ConfigurationLoader.Load(_temp.CreateFileWithContent("c.json", """{ "cognitive": { "scoreThreshold": 1 } }"""));

        Assert.That(configuration.Warnings, Is.Empty);
        Assert.That(configuration.Metrics.Keys, Is.EquivalentTo(ScoreCalculator.SupportedMetricKeys));
    }
}
