/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysisConsoleApp.Infrastructure;

public static class ConfigFileGenerator
{
    /// <summary>
    /// Writes the default settings. A <paramref name="target"/> that ends in <c>.json</c> and is not an existing
    /// directory is the file to write; anything else is the directory that receives the default file name.
    /// </summary>
    public static string Generate(string target)
    {
        bool isFile = target.EndsWith(".json", StringComparison.OrdinalIgnoreCase) && !Directory.Exists(target);
        string outputPath = isFile ? Path.GetFullPath(target) : Path.Combine(target, ConfigurationResolver.DefaultFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        File.WriteAllText(outputPath, CognitiveConfigurationDefaults.SettingsJson);
        return outputPath;
    }
}
