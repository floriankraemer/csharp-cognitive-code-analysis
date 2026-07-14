/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Application;

public static class CognitiveConfigurationFactory
{
    public static CognitiveConfiguration Load(string? configFile, AnalysisDisplayOverrides? overrides = null)
    {
        var (configuration, _) = LoadWithSource(configFile, overrides);
        return configuration;
    }

    public static (CognitiveConfiguration Configuration, ConfigSource Source) LoadWithSource(
        string? configFile,
        AnalysisDisplayOverrides? overrides = null
    ) {
        var source = ConfigurationResolver.Resolve(configFile);
        var configuration = source.IsDefault
            ? CognitiveConfigurationDefaults.Create()
            : ConfigurationLoader.Load(source.Path);
        ApplyDisplayOverrides(configuration, overrides);
        return (configuration, source);
    }

    private static void ApplyDisplayOverrides(
        CognitiveConfiguration configuration,
        AnalysisDisplayOverrides? overrides
    ) {
        if (overrides == null)
        {
            return;
        }

        if (overrides.ShowHalstead.HasValue)
        {
            configuration.ShowHalsteadComplexity = overrides.ShowHalstead.Value;
        }

        if (overrides.ShowCyclomatic.HasValue)
        {
            configuration.ShowCyclomaticComplexity = overrides.ShowCyclomatic.Value;
        }

        if (overrides.ShowCoupling.HasValue)
        {
            configuration.ShowCouplingMetrics = overrides.ShowCoupling.Value;
        }
    }
}
