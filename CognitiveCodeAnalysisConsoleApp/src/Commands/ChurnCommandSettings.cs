/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.ComponentModel;
using System.Globalization;

using CognitiveCodeAnalysis.Churn;

using Spectre.Console;
using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Commands;

internal sealed class ChurnCommandSettings : CommandSettings
{
    public const string DefaultSince = "2000-01-01";
    public const string SupportedVcs = "git";
    private const string DateFormat = "yyyy-MM-dd";

    [Description("Path to search for c# files. Defaults to current path.")]
    [CommandArgument(0, "[path]")]
    public string? SourcePath { get; init; }

    [Description("Count the changes since the start of this day (yyyy-MM-dd)")]
    [CommandOption("-s|--since")]
    [DefaultValue(DefaultSince)]
    public string Since { get; init; } = DefaultSince;

    [Description("Version control system. Only git is supported")]
    [CommandOption("--vcs")]
    [DefaultValue(SupportedVcs)]
    public string Vcs { get; init; } = SupportedVcs;

    [Description("Report type: Json, Csv, Html, Markdown, SvgTreemap (any letter case). Requires --output-file. Defaults to a console table.")]
    [CommandOption("-r|--report-type")]
    public string? ReportType { get; init; }

    [Description("File the report is written to. Requires --report-type")]
    [CommandOption("-o|--output-file")]
    public string? OutputFile { get; init; }

    [Description("Load a custom configuration")]
    [CommandOption("-c|--config")]
    public string? ConfigFile { get; init; }

    [Description("Path to Cobertura coverage report file; adds risk churn, coverage and risk level")]
    [CommandOption("--coverage-cobertura")]
    public string? CoverageCobertura { get; init; }

    [Description("Path to Clover coverage report file; adds risk churn, coverage and risk level")]
    [CommandOption("--coverage-clover")]
    public string? CoverageClover { get; init; }

    public DateOnly SinceDate => ParseSince(Since)!.Value;

    public ChurnReportType? ReportTypeValue =>
        ChurnReportTypes.TryParse(ReportType, out ChurnReportType type) ? type : null;

    public override ValidationResult Validate()
    {
        if (!CoverageOptions.AreExclusive(CoverageCobertura, CoverageClover))
        {
            return ValidationResult.Error(CoverageOptions.OnlyOneFormatMessage);
        }

        if (ParseSince(Since) is null)
        {
            return ValidationResult.Error($"Invalid --since date '{Since}'. Expected format: {DateFormat}.");
        }

        if (!string.Equals(Vcs, SupportedVcs, StringComparison.OrdinalIgnoreCase))
        {
            return ValidationResult.Error($"Unknown VCS '{Vcs}'. Supported: {SupportedVcs}.");
        }

        if (ReportType is not null && ReportTypeValue is null)
        {
            return ValidationResult.Error(
                $"Invalid report type '{ReportType}'. Expected one of: {ChurnReportTypes.Accepted}.");
        }

        if ((ReportType is null) != (OutputFile is null))
        {
            return ValidationResult.Error("--report-type and --output-file must be given together.");
        }

        return ValidationResult.Success();
    }

    private static DateOnly? ParseSince(string value) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date)
            ? date
            : null;
}
