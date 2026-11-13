/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using Microsoft.Extensions.Configuration;

using System.IO;

namespace CognitiveCodeAnalysis.Configuration;

public static class ConfigurationLoader
{
    /// <summary>
    /// <![CDATA[
    /// Loads configuration directly and returns the CognitiveConfiguration object.
    /// Unknown metric keys are dropped and reported in <see cref="CognitiveConfiguration.Warnings"/>.
    /// ]]>
    /// </summary>
    public static CognitiveConfiguration Load(string? configFilePath = null)
    {
        IConfigurationRoot configuration = BuildConfiguration(configFilePath);

        CognitiveConfiguration cognitiveConfig = CognitiveConfigurationDefaults.Create();
        configuration.GetSection("cognitive").Bind(cognitiveConfig);
        cognitiveConfig.Warnings = MetricKeyValidator.Normalize(cognitiveConfig);

        return cognitiveConfig;
    }

    private static IConfigurationRoot BuildConfiguration(string? configFilePath = null)
    {
        ConfigurationBuilder builder = new();

        if (string.IsNullOrEmpty(configFilePath)) return GetDefaultConfig();

        string fullConfigPath = Path.GetFullPath(configFilePath);
        string directory = Path.GetDirectoryName(fullConfigPath) ?? AppContext.BaseDirectory;
        string fileName = Path.GetFileName(fullConfigPath);

        return builder.SetBasePath(directory)
                .AddJsonFile(fileName, optional: false, reloadOnChange: false)
                .Build();
    }

    private static IConfigurationRoot GetDefaultConfig()
    {
        ConfigurationBuilder builder = new();

        return builder.SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("cognitive-metrics-settings.json", optional: true, reloadOnChange: false)
                    .Build();
    }
}
