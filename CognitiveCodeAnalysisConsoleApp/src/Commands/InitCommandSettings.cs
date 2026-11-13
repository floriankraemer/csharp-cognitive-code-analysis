/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.ComponentModel;

using CognitiveCodeAnalysis.Configuration;

using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Commands;

internal sealed class InitCommandSettings : CommandSettings
{
    public const string DefaultPath = "./" + ConfigurationResolver.DefaultFileName;

    [Description("Config file to create. Default: ./cognitive-metrics-settings.json")]
    [CommandOption("-p|--path")]
    [DefaultValue(DefaultPath)]
    public string Path { get; init; } = DefaultPath;

    [Description("Skip the questions and write the default values")]
    [CommandOption("--silent")]
    public bool Silent { get; init; }

    [Description("Overwrite an existing config file")]
    [CommandOption("-f|--force")]
    public bool Force { get; init; }
}
