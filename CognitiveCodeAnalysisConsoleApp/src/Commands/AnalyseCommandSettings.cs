/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.ComponentModel;

using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Commands;

internal sealed class AnalyseCommandSettings : CommandSettings
{
    // An attribute argument must be a constant; a test keeps this list equal to MetricsSorter.FieldNames.
    internal const string SortFields =
        "score, class, method, file, line, halstead, cyclomatic, "
        + "linesOfCode, argumentCount, returnCount, localVariableCount, propertyAccessCount, fieldAccessCount, "
        + "ifCount, elseCount, nestingLevels, loopCount, switchCount, tryCatchCount, "
        + "linesOfCodeScore, argumentCountScore, returnCountScore, localVariableCountScore, propertyAccessCountScore, "
        + "fieldAccessCountScore, ifCountScore, elseCountScore, nestingLevelsScore, loopCountScore, switchCountScore, "
        + "tryCatchCountScore";

    [Description("C# file, directory, or comma-separated list of both to analyse. Directories are searched recursively. Defaults to the current path.")]
    [CommandArgument(0, "[searchPath]")]
    public string? SourcePath { get; init; }

    [Description("Load a custom configuration")]
    [CommandOption("-c|--config")]
    public string? ConfigFile { get; init; }

    [Description("Write a default cognitive-metrics-settings.json to PATH or the current directory, then exit")]
    [CommandOption("--generate-config [PATH]")]
    public required FlagValue<string?> GenerateConfig { get; init; }

    [Description("Report type: ConsoleText, Html, Markdown, Json, Sarif, GithubActions, GitlabCodeQuality, Checkstyle, JUnit, Csv. Defaults to console.")]
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

    [Description("Sort the rows of the reports by a field. Without it every report keeps its default order. Fields: " + SortFields + ".")]
    [CommandOption("--sort-by <FIELD>")]
    public string? SortBy { get; init; }

    [Description("Sort order for --sort-by: asc or desc")]
    [CommandOption("--sort-order <ORDER>")]
    [DefaultValue("asc")]
    public string SortOrder { get; init; } = "asc";

    [Description("Print the total time, the time of each phase and statistics to stderr after the run")]
    [CommandOption("--debug")]
    public bool Debug { get; init; }

    [Description("Emit timestamped diagnostic logs to stderr for each analysis phase")]
    [CommandOption("-v|--verbose")]
    public bool Verbose { get; init; }
}
