using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CognitiveCodeAnalysis.Configuration;

public static class ConfigurationLoader
{
    /// <summary>
    /// Loads configuration directly and returns the CognitiveConfiguration object.
    /// </summary>
    public static CognitiveConfiguration Load(string? configFilePath = null)
    {
        IConfigurationRoot configuration = BuildConfiguration(configFilePath);

        CognitiveConfiguration cognitiveConfig = new();
        configuration.GetSection("cognitive").Bind(cognitiveConfig);

        return cognitiveConfig;
    }

    /// <summary>
    /// Builds and configures services with IOptions&lt;CognitiveConfiguration&gt; pattern.
    /// Returns a service provider that can be used to resolve IOptions&lt;CognitiveConfiguration&gt;.
    /// </summary>
    public static IServiceProvider ConfigureServices(string? configFilePath = null)
    {
        IConfigurationRoot configuration = BuildConfiguration(configFilePath);

        IServiceCollection services = new ServiceCollection();
        services.Configure<CognitiveConfiguration>(configuration.GetSection("cognitive"));

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Gets the CognitiveConfiguration using IOptions<T> pattern.
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
                    .AddJsonFile("cognitive-metrics-settings.json", optional: false, reloadOnChange: false)
                    .Build();
    }
}
