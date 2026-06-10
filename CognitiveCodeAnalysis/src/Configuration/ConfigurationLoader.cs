/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using Microsoft.CodeAnalysis;

using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading;

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

    /// <summary>
    /// <![CDATA[
    /// Builds and configures services with IOptions&lt;CognitiveConfiguration&gt; pattern.
    /// Returns a service provider that can be used to resolve IOptions&lt;CognitiveConfiguration&gt;.
    /// ]]>
    /// </summary>
    public static IServiceProvider ConfigureServices(string? configFilePath = null)
    {
        IConfigurationRoot configuration = BuildConfiguration(configFilePath);

        IServiceCollection services = new ServiceCollection();
        services.Configure<CognitiveConfiguration>(configuration.GetSection("cognitive"));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// <![CDATA[
    /// Gets the CognitiveConfiguration using IOptions<T> pattern.
    /// ]]>
    /// </summary>
    public static CognitiveConfiguration GetConfiguration(IServiceProvider serviceProvider)
    {
        IOptions<CognitiveConfiguration> options = serviceProvider.GetRequiredService<IOptions<CognitiveConfiguration>>();

        return options.Value;
    }

    private static IConfigurationRoot BuildConfiguration(string? configFilePath = null)
    {
        ConfigurationBuilder builder = new();

        if (string.IsNullOrEmpty(configFilePath)) return GetDefaultConfig();

        // If a specific file path is provided, use it directly
        string directory = Path.GetDirectoryName(configFilePath) ?? AppContext.BaseDirectory;
        string fileName = Path.GetFileName(configFilePath);

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
    internal const string DefaultCognitiveMetricsSettingsJson =
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

    /// <summary>
    /// Layers optional JSON overlays (CLI schema) onto <see cref="DefaultCognitiveMetricsSettingsJson"/> (same merging as chained JSON providers).
    /// </summary>
    public static CognitiveConfiguration LoadFromJsonLayers(params string?[] overlays)
    {
        ConfigurationBuilder builder = new();
        builder.AddJsonStream(CreateJsonStream(DefaultCognitiveMetricsSettingsJson));

        foreach (string? overlay in overlays)
        {
            if (string.IsNullOrWhiteSpace(overlay))
            {
                continue;
            }

            try
            {
                builder.AddJsonStream(CreateJsonStream(overlay!));
            }
            catch
            {
                continue;
            }
        }

        return BindFromCognitiveSection(builder.Build());
    }

    /// <summary>
    /// Resolves analyzer settings like the CLI does: bundled defaults layered with optional
    /// <c>cognitive-metrics-settings.json</c> supplied as compilation <see cref="AdditionalText"/> inputs.
    /// </summary>
    public static CognitiveConfiguration LoadCognitiveConfigurationForAnalyzer(
        ImmutableArray<AdditionalText> additionalFiles,
        CancellationToken cancellationToken = default)
    {
        ConfigurationBuilder builder = new();
        builder.AddJsonStream(CreateJsonStream(DefaultCognitiveMetricsSettingsJson));

        foreach (AdditionalText text in additionalFiles)
        {
            if (!string.Equals(Path.GetFileName(text.Path), "cognitive-metrics-settings.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                Microsoft.CodeAnalysis.Text.SourceText? sourceText = text.GetText(cancellationToken);
                if (sourceText == null)
                {
                    continue;
                }

                builder.AddJsonStream(CreateJsonStream(sourceText.ToString()));
            }
            catch
            {
                continue;
            }
        }

        return BindFromCognitiveSection(builder.Build());
    }

    private static CognitiveConfiguration BindFromCognitiveSection(IConfiguration configuration)
    {
        CognitiveConfiguration config = new();
        try
        {
            configuration.GetSection("cognitive").Bind(config);
        }
        catch
        {
            // Fall back to defaults if binding fails (invalid JSON shape).
            new ConfigurationBuilder()
                .AddJsonStream(CreateJsonStream(DefaultCognitiveMetricsSettingsJson))
                .Build()
                .GetSection("cognitive")
                .Bind(config);
        }

        return config;
    }

    private static MemoryStream CreateJsonStream(string json)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(json)) { Position = 0 };
    }
}
