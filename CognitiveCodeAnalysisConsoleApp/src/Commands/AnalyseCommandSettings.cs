/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.ComponentModel;

using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Commands;

internal sealed class AnalyseCommandSettings : CommandSettings
{
    [Description("Path to search for c# files. Defaults to current path.")]
    [CommandArgument(0, "[searchPath]")]
    public string? SourcePath { get; init; }

    [Description("Load a custom configuration")]
    [CommandOption("-c|--config")]
    public string? ConfigFile { get; init; }

    [Description("Write a default cognitive-metrics-settings.json to [path] or the current directory, then exit")]
    [CommandOption("--generate-config [path]")]
    public FlagValue<string?> GenerateConfig { get; init; }

    [Description("Report type: ConsoleText, Html, Markdown, Json, Sarif, GithubActions, GitlabCodeQuality, Csv. Defaults to console.")]
    [CommandOption("-f|-r|--report-type|--report-format")]
    [DefaultValue("ConsoleText")]
    public string? ReportType { get; init; }

    [Description("Path to a JSON baseline snapshot for delta comparison")]
    [CommandOption("-b|--baseline")]
    public string? BaselineFile { get; init; }

    [Description("Output file")]
    [CommandOption("-o|--output-file")]
    public string? OutputFile { get; init; }

    [Description("Path to Cobertura coverage report file")]
    [CommandOption("--coverage-cobertura")]
    public string? CoverageCobertura { get; init; }

    [Description("Show Halstead volume/difficulty/effort in reports (overrides config when set)")]
    [CommandOption("--show-halstead")]
    public bool? ShowHalstead { get; init; }

    [Description("Show cyclomatic complexity in reports (overrides config when set)")]
    [CommandOption("--show-cyclomatic")]
    public bool? ShowCyclomatic { get; init; }

    [Description("Show class coupling metrics in reports (overrides config when set)")]
    [CommandOption("--show-coupling")]
    public bool? ShowCoupling { get; init; }
}
