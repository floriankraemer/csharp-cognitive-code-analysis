/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Configuration;

public sealed record ConfigSource(bool IsDefault, string? Path)
{
    public string Display => IsDefault ? "Default" : Path!;
}

public static class ConfigurationResolver
{
    public const string DefaultFileName = "cognitive-metrics-settings.json";

    public static ConfigSource Resolve(string? explicitConfigPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitConfigPath))
        {
            return new ConfigSource(false, Path.GetFullPath(explicitConfigPath));
        }

        string cwdFile = Path.Combine(Directory.GetCurrentDirectory(), DefaultFileName);
        if (File.Exists(cwdFile))
        {
            return new ConfigSource(false, Path.GetFullPath(cwdFile));
        }

        return new ConfigSource(true, null);
    }
}
