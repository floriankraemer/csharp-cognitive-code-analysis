/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.Application;

public class CognitiveConfigurationFactoryTests
{
    private string _tempDirectory = null!;
    private string _originalWorkingDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _originalWorkingDirectory = Directory.GetCurrentDirectory();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cogcfg-factory-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
        Directory.SetCurrentDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_originalWorkingDirectory);

        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Test]
    public void Load_WithoutOverrides_KeepsDefaultDisplayFlags()
    {
        var configuration = CognitiveConfigurationFactory.Load(configFile: null);

        Assert.That(configuration.ShowHalsteadComplexity, Is.False);
        Assert.That(configuration.ShowCyclomaticComplexity, Is.False);
        Assert.That(configuration.ShowCouplingMetrics, Is.False);
    }

    [Test]
    public void Load_WithOverrides_AppliesOnlySpecifiedFlags()
    {
        var overrides = new AnalysisDisplayOverrides(
            ShowHalstead: true,
            ShowCyclomatic: null,
            ShowCoupling: false
        );

        var configuration = CognitiveConfigurationFactory.Load(configFile: null, overrides);

        Assert.That(configuration.ShowHalsteadComplexity, Is.True);
        Assert.That(configuration.ShowCyclomaticComplexity, Is.False);
        Assert.That(configuration.ShowCouplingMetrics, Is.False);
    }

    [Test]
    public void Load_WithCyclomaticOverride_EnablesCyclomaticDisplay()
    {
        var overrides = new AnalysisDisplayOverrides(ShowHalstead: null, ShowCyclomatic: true, ShowCoupling: null);

        var configuration = CognitiveConfigurationFactory.Load(configFile: null, overrides);

        Assert.That(configuration.ShowCyclomaticComplexity, Is.True);
    }

    [Test]
    public void LoadWithSource_NoFile_ReturnsDefaultsAndDefaultSource()
    {
        var (configuration, source) = CognitiveConfigurationFactory.LoadWithSource(configFile: null);

        Assert.That(source.IsDefault, Is.True);
        Assert.That(source.Display, Is.EqualTo("Default"));
        Assert.That(configuration.ScoreThreshold, Is.EqualTo(0.5));
        Assert.That(configuration.ShowHalsteadComplexity, Is.False);
    }

    [Test]
    public void LoadWithSource_CwdFile_AppliesFileValuesAndReturnsPath()
    {
        var cwdFile = Path.Combine(_tempDirectory, ConfigurationResolver.DefaultFileName);
        File.WriteAllText(
            cwdFile,
            """
            {
              "cognitive": {
                "scoreThreshold": 9.87,
                "showHalsteadComplexity": true
              }
            }
            """
        );

        var (configuration, source) = CognitiveConfigurationFactory.LoadWithSource(configFile: null);

        Assert.That(source.IsDefault, Is.False);
        Assert.That(source.Path, Is.EqualTo(Path.GetFullPath(cwdFile)));
        Assert.That(configuration.ScoreThreshold, Is.EqualTo(9.87));
        Assert.That(configuration.ShowHalsteadComplexity, Is.True);
    }

    [Test]
    public void LoadWithSource_ExplicitFile_AppliesFileValuesAndReturnsPath()
    {
        var explicitFile = Path.Combine(_tempDirectory, "custom.json");
        File.WriteAllText(
            explicitFile,
            """
            {
              "cognitive": {
                "scoreThreshold": 4.56,
                "showCouplingMetrics": true
              }
            }
            """
        );

        var (configuration, source) = CognitiveConfigurationFactory.LoadWithSource(configFile: explicitFile);

        Assert.That(source.IsDefault, Is.False);
        Assert.That(source.Path, Is.EqualTo(Path.GetFullPath(explicitFile)));
        Assert.That(configuration.ScoreThreshold, Is.EqualTo(4.56));
        Assert.That(configuration.ShowCouplingMetrics, Is.True);
    }
}
