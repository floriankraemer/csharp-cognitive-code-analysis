/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.Configuration;

public class ConfigurationDefaultsTests
{
    private static string ShippedFile => Path.Combine(AppContext.BaseDirectory, ConfigurationResolver.DefaultFileName);

    [Test]
    public void SettingsJson_IsTheShippedFileVerbatim()
    {
        Assert.That(CognitiveConfigurationDefaults.SettingsJson, Is.EqualTo(File.ReadAllText(ShippedFile)));
    }

    [Test]
    public void Create_AndLoadingTheShippedFile_AgreeOnEveryValue()
    {
        var defaults = CognitiveConfigurationDefaults.Create();
        var shipped = ConfigurationLoader.Load(ShippedFile);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(defaults.ScoreThreshold, Is.EqualTo(shipped.ScoreThreshold));
            Assert.That(defaults.ShowDetailedCognitiveMetrics, Is.EqualTo(shipped.ShowDetailedCognitiveMetrics));
            Assert.That(defaults.GroupByClass, Is.EqualTo(shipped.GroupByClass));
            Assert.That(defaults.ShowCouplingMetrics, Is.EqualTo(shipped.ShowCouplingMetrics));
            Assert.That(defaults.Metrics.Keys, Is.EquivalentTo(shipped.Metrics.Keys));
            foreach ((string key, MetricConfiguration metric) in defaults.Metrics)
            {
                Assert.That(metric.Enabled, Is.EqualTo(shipped.Metrics[key].Enabled), key);
                Assert.That(metric.Threshold, Is.EqualTo(shipped.Metrics[key].Threshold), key);
                Assert.That(metric.Scale, Is.EqualTo(shipped.Metrics[key].Scale), key);
            }
        }
    }

    [Test]
    public void Create_UsesOnlyKeysThatCanBeScored_AndKeepsTheExtraMetricsDisabled()
    {
        var defaults = CognitiveConfigurationDefaults.Create();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(defaults.Metrics.Keys, Is.EquivalentTo(ScoreCalculator.SupportedMetricKeys));
            Assert.That(defaults.Warnings, Is.Empty);
            foreach (string disabled in new[] { "localVariableCount", "propertyAccessCount", "fieldAccessCount", "loopCount", "switchCount", "tryCatchCount" })
            {
                Assert.That(defaults.Metrics[disabled].Enabled, Is.False, disabled);
            }

            foreach (string enabled in new[] { "linesOfCode", "argumentCount", "returnCount", "ifCount", "nestingLevels", "elseCount" })
            {
                Assert.That(defaults.Metrics[enabled].Enabled, Is.True, enabled);
            }
        }
    }

    [Test]
    public void ShowDetailedCognitiveMetrics_DefaultsToTrue_AndIsBoundFromTheFile()
    {
        var temp = new TempFiles();
        try
        {
            var off = ConfigurationLoader.Load(temp.CreateFileWithContent("off.json", """{ "cognitive": { "showDetailedCognitiveMetrics": false } }"""));
            var untouched = ConfigurationLoader.Load(temp.CreateFileWithContent("none.json", """{ "cognitive": { } }"""));

            Assert.That(CognitiveConfigurationDefaults.Create().ShowDetailedCognitiveMetrics, Is.True);
            Assert.That(off.ShowDetailedCognitiveMetrics, Is.False);
            Assert.That(untouched.ShowDetailedCognitiveMetrics, Is.True);
        }
        finally
        {
            temp.CleanUp();
        }
    }
}
