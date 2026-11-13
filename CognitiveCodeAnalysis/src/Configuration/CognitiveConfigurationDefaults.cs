/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Reflection;
using System.Text;

using Microsoft.Extensions.Configuration;

namespace CognitiveCodeAnalysis.Configuration;

/// <summary>
/// The built-in defaults. The shipped <c>cognitive-metrics-settings.json</c> is the only source:
/// it is embedded in the assembly, parsed here, and written out unchanged by <c>--generate-config</c>.
/// </summary>
public static class CognitiveConfigurationDefaults
{
    private const string ResourceName = "cognitive-metrics-settings.json";

    /// <summary>
    /// The text of the shipped settings file.
    /// </summary>
    public static string SettingsJson { get; } = ReadSettingsJson();

    public static CognitiveConfiguration Create()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SettingsJson));
        IConfigurationRoot root = new ConfigurationBuilder().AddJsonStream(stream).Build();

        var configuration = new CognitiveConfiguration();
        root.GetSection("cognitive").Bind(configuration);

        return configuration;
    }

    private static string ReadSettingsJson()
    {
        using Stream stream = typeof(CognitiveConfigurationDefaults).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
}
