/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;

namespace CognitiveCodeAnalysis.Tests.Application;

public class CognitiveConfigurationFactoryTests
{
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
}
