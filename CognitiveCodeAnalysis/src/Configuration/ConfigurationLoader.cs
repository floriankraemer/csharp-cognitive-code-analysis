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
    /// ]]>
    /// </summary>
    public static CognitiveConfiguration Load(string? configFilePath = null)
    {
        IConfigurationRoot configuration = BuildConfiguration(configFilePath);

        CognitiveConfiguration cognitiveConfig = CognitiveConfigurationDefaults.Create();
        configuration.GetSection("cognitive").Bind(cognitiveConfig);

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

    /// <summary>
    /// Default cognitive settings (same defaults as bundled cognitive-metrics-settings.json).
    /// Kept embedded so analyzer hosts that do not set AppContext.BaseDirectory to the CLI output still get CLI parity.
    /// </summary>
    public const string DefaultCognitiveMetricsSettingsJson =
        """
{
    "cognitive": {
        "excludeFilePatterns": [],
        "excludePatterns": [],
        "scoreThreshold": 0.5,
        "showOnlyMethodsExceedingThreshold": true,
        "showHalsteadComplexity": false,
        "showCyclomaticComplexity": false,
        "showDetailedCognitiveMetrics": true,
        "groupByClass": true,
        "countElseAsNesting": false,
        "countElseIfAsNesting": false,
        "metrics": {
            "linesOfCode": { "threshold": 60, "scale": 25.0, "enabled": true },
            "argumentCount": { "threshold": 4, "scale": 1.0, "enabled": true },
            "returnCount": { "threshold": 2, "scale": 5.0, "enabled": true },
            "localVariableCount": { "threshold": 4, "scale": 5.0, "enabled": true },
            "propertyAccessCount": { "threshold": 4, "scale": 15.0, "enabled": true },
            "ifCount": { "threshold": 3, "scale": 1.0, "enabled": true },
            "nestingLevels": { "threshold": 1, "scale": 1.0, "enabled": true },
            "elseCount": { "threshold": 1, "scale": 1.0, "enabled": true }
        }
    }
}
""";
}
