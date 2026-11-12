/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;


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
    public void Load_RelativeConfigPath_ResolvesAgainstCurrentDirectory()
    {
        var json = """
            {
              "cognitive": {
                "scoreThreshold": 3.21
              }
            }
            """;
        var dir = Path.Combine(Path.GetTempPath(), "cogcfg-rel-" + Guid.NewGuid());
        var subDir = Path.Combine(dir, "config");
        Directory.CreateDirectory(subDir);
        var file = Path.Combine(subDir, "custom.json");
        var originalCwd = Directory.GetCurrentDirectory();
        try
        {
            File.WriteAllText(file, json);
            Directory.SetCurrentDirectory(dir);
            var cfg = ConfigurationLoader.Load(Path.Combine("config", "custom.json"));
            Assert.That(cfg.ScoreThreshold, Is.EqualTo(3.21));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCwd);
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
}
