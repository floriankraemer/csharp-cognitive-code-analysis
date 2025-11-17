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
        IConfigurationRoot configuration = BuildConfiguration();

        CognitiveConfiguration cognitiveConfig = new();
        configuration.GetSection("cognitive").Bind(cognitiveConfig);

        return cognitiveConfig;
    }

    /// <summary>
    /// Builds and configures services with IOptions<CognitiveConfiguration> pattern.
    /// Returns a service provider that can be used to resolve IOptions<CognitiveConfiguration>.
    /// </summary>
    public static IServiceProvider ConfigureServices()
    {
        IConfigurationRoot configuration = BuildConfiguration();

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

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();
    }
}
