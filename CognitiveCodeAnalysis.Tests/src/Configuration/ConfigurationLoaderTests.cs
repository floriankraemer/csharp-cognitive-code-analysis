/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.Configuration;

public class ConfigurationLoaderTests
{
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
}
