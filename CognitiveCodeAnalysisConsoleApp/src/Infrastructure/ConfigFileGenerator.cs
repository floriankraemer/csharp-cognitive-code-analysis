/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysisConsoleApp.Infrastructure;

public static class ConfigFileGenerator
{
    public static string Generate(string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        string outputPath = Path.Combine(targetDirectory, ConfigurationResolver.DefaultFileName);
        File.WriteAllText(outputPath, ConfigurationLoader.DefaultCognitiveMetricsSettingsJson);
        return outputPath;
    }
}
