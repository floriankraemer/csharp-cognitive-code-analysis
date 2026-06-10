/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Collections.Immutable;

using CognitiveCodeAnalysis.Configuration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace CognitiveCodeAnalysis.Tests.Configuration;

public class ConfigurationLoaderTests
{
    [Test]
    public void CreateDefaultConfiguration_MatchesShippedDefaults()
    {
        var cfg = CognitiveConfigurationDefaults.Create();

        Assert.That(cfg.ScoreThreshold, Is.EqualTo(0.5));
        Assert.That(cfg.GroupByClass, Is.True);
        Assert.That(cfg.ShowOnlyMethodsExceedingThreshold, Is.True);
        Assert.That(cfg.Metrics, Contains.Key("ifCount"));
        Assert.That(cfg.Metrics["ifCount"].Enabled, Is.True);
        Assert.That(cfg.Metrics["loopCount"].Enabled, Is.False);
    }

    [Test]
    public void Load_CustomJsonFile_AppliesScoreThreshold()
    {
        var json = """
            {
              "cognitive": {
                "scoreThreshold": 12.34,
                "showOnlyMethodsExceedingThreshold": false,
                "groupByClass": true
              }
            }
            """;
        var dir = Path.Combine(Path.GetTempPath(), "cogcfg-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "custom.json");
        try
        {
            File.WriteAllText(file, json);
            var cfg = ConfigurationLoader.Load(file);
            Assert.That(cfg.ScoreThreshold, Is.EqualTo(12.34));
            Assert.That(cfg.ShowOnlyMethodsExceedingThreshold, Is.False);
            Assert.That(cfg.GroupByClass, Is.True);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Test]
    public void ConfigureServices_GetConfiguration_RoundTrips()
    {
        var json = """
            { "cognitive": { "scoreThreshold": 7.5, "showOnlyMethodsExceedingThreshold": true } }
            """;
        var dir = Path.Combine(Path.GetTempPath(), "cogcfg2-" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "opts.json");
        try
        {
            File.WriteAllText(file, json);
            var sp = ConfigurationLoader.ConfigureServices(file);
            try
            {
                var cfg = ConfigurationLoader.GetConfiguration(sp);
                Assert.That(cfg.ScoreThreshold, Is.EqualTo(7.5));
            }
            finally
            {
                (sp as IDisposable)?.Dispose();
            }
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Test]
    public void Load_WithoutConfigFile_UsesBundledDefaults()
    {
        var cfg = ConfigurationLoader.Load();

        Assert.That(cfg.ScoreThreshold, Is.EqualTo(0.5));
        Assert.That(cfg.ShowOnlyMethodsExceedingThreshold, Is.True);
        Assert.That(cfg.Metrics["linesOfCode"].Threshold, Is.EqualTo(60));
    }

    [Test]
    public void LoadFromJsonLayers_AppliesOverlayOnTopOfDefaults()
    {
        var cfg = ConfigurationLoader.LoadFromJsonLayers(
            null,
            "   ",
            """
            {
              "cognitive": {
                "scoreThreshold": 0.9,
                "groupByClass": false
              }
            }
            """
        );

        Assert.That(cfg.ScoreThreshold, Is.EqualTo(0.9));
        Assert.That(cfg.GroupByClass, Is.False);
        Assert.That(cfg.Metrics["ifCount"].Enabled, Is.True);
    }

    [Test]
    public void LoadFromJsonLayers_InvalidOverlay_FallsBackToDefaults()
    {
        var cfg = ConfigurationLoader.LoadFromJsonLayers(
            """{ "cognitive": { "scoreThreshold": 0.25 } }""",
            """{ "cognitive": "invalid-shape" }"""
        );

        Assert.That(cfg.ScoreThreshold, Is.EqualTo(0.25));
        Assert.That(cfg.GroupByClass, Is.True);
    }

    [Test]
    public void LoadCognitiveConfigurationForAnalyzer_AppliesMatchingAdditionalText()
    {
        var additional = ImmutableArray.Create<AdditionalText>(
            new StubAdditionalText("/ignored/other.json", """{ "cognitive": { "scoreThreshold": 0.1 } }"""),
            new StubAdditionalText("/settings/cognitive-metrics-settings.json", """
                { "cognitive": { "scoreThreshold": 0.75, "showHalsteadComplexity": true } }
                """)
        );

        var cfg = ConfigurationLoader.LoadCognitiveConfigurationForAnalyzer(additional);

        Assert.That(cfg.ScoreThreshold, Is.EqualTo(0.75));
        Assert.That(cfg.ShowHalsteadComplexity, Is.True);
    }

    [Test]
    public void LoadCognitiveConfigurationForAnalyzer_IgnoresUnreadableAdditionalText()
    {
        var additional = ImmutableArray.Create<AdditionalText>(
            new StubAdditionalText("/settings/cognitive-metrics-settings.json", content: null)
        );

        var cfg = ConfigurationLoader.LoadCognitiveConfigurationForAnalyzer(additional);

        Assert.That(cfg.ScoreThreshold, Is.EqualTo(0.5));
    }

    private sealed class StubAdditionalText(string path, string? content) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText? GetText(CancellationToken cancellationToken = default)
            => content is null ? null : SourceText.From(content);
    }
}
