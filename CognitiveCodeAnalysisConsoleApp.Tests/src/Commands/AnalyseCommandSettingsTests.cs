/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysisConsoleApp.Application;
using CognitiveCodeAnalysisConsoleApp.Commands;
using CognitiveCodeAnalysisConsoleApp.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Commands;

public class AnalyseCommandSettingsTests
{
    [SetUp]
    public void SetUp() => CliParseProbeCommand.Reset();

    [TestCase("-f", "Html")]
    [TestCase("-r", "Json")]
    [TestCase("--report-format", "Markdown")]
    [TestCase("--report-type", "Csv")]
    public void ParseReportFormatOptions_BindsReportType(string flag, string reportType)
    {
        Parse($".\\src {flag} {reportType} -o .\\report.out");

        Assert.That(CliParseProbeCommand.Parsed, Is.Not.Null);
        Assert.That(CliParseProbeCommand.Parsed!.ReportType, Is.EqualTo(reportType));
    }

    [Test]
    public void ParseSortAndDebugOptions_BindsThem()
    {
        Parse(".\\src --sort-by line --sort-order desc --debug");

        var parsed = CliParseProbeCommand.Parsed!;
        Assert.That(parsed.SortBy, Is.EqualTo("line"));
        Assert.That(parsed.SortOrder, Is.EqualTo("desc"));
        Assert.That(parsed.Debug, Is.True);
    }

    [Test]
    public void ParseWithoutSortAndDebugOptions_UsesTheDefaults()
    {
        Parse(".\\src");

        var parsed = CliParseProbeCommand.Parsed!;
        Assert.That(parsed.SortBy, Is.Null);
        Assert.That(parsed.SortOrder, Is.EqualTo("asc"));
        Assert.That(parsed.Debug, Is.False);
    }

    [Test]
    public void SortFieldsHelp_ListsExactlyTheFieldsTheSorterAccepts()
    {
        Assert.That(
            AnalyseCommandSettings.SortFields.Split(", "),
            Is.EqualTo(CognitiveCodeAnalysis.CognitiveAnalysis.MetricsSorter.FieldNames));
    }

    [TestCase("-o", ".\\report.html")]
    [TestCase("--output-file", ".\\report.json")]
    public void ParseOutputOptions_BindsOutputFile(string flag, string outputFile)
    {
        Parse($".\\src -f Html {flag} {outputFile}");

        Assert.That(CliParseProbeCommand.Parsed, Is.Not.Null);
        Assert.That(CliParseProbeCommand.Parsed!.OutputFile, Is.EqualTo(outputFile));
    }

    [TestCase("-g", true)]
    [TestCase("--generate-baseline", true)]
    [TestCase("", false)]
    public void ParseGenerateBaselineOption_BindsTheFlag(string flag, bool expected)
    {
        Parse($".\\src {flag}");

        Assert.That(CliParseProbeCommand.Parsed!.GenerateBaseline, Is.EqualTo(expected));
    }

    [Test]
    public void ParseShortReportFormatAndOutputOptions_MapsToAnalysisRequest()
    {
        Parse(".\\src -f Html -o .\\report.html");

        Assert.That(CliParseProbeCommand.Parsed, Is.Not.Null);

        AnalysisRequest request = AnalyseRequestMapper.FromSettings(CliParseProbeCommand.Parsed!);

        Assert.That(request.ReportType, Is.EqualTo("Html"));
        Assert.That(request.OutputFile, Is.EqualTo(".\\report.html"));
    }

    [Test]
    public void ParseLongReportFormatAndOutputOptions_MapsToAnalysisRequest()
    {
        Parse(".\\src --report-format Html --output-file .\\report.html");

        AnalysisRequest request = AnalyseRequestMapper.FromSettings(CliParseProbeCommand.Parsed!);

        Assert.That(request.ReportType, Is.EqualTo("Html"));
        Assert.That(request.OutputFile, Is.EqualTo(".\\report.html"));
    }

    [Test]
    public void ParseGenerateConfigWithoutPath_SetsGenerateConfigFlag()
    {
        Parse("--generate-config");

        Assert.That(CliParseProbeCommand.Parsed, Is.Not.Null);
        Assert.That(CliParseProbeCommand.Parsed!.GenerateConfig.IsSet, Is.True);
        Assert.That(CliParseProbeCommand.Parsed.GenerateConfig.Value, Is.Null);
    }

    [Test]
    public void ParseVerboseFlag_MapsToAnalysisRequest()
    {
        Parse(".\\src -f Html --verbose");

        AnalysisRequest request = AnalyseRequestMapper.FromSettings(CliParseProbeCommand.Parsed!);

        Assert.That(request.Verbose, Is.True);
    }

    [Test]
    public void ParseGenerateConfigWithPath_BindsDirectory()
    {
        Parse("--generate-config .\\config");

        Assert.That(CliParseProbeCommand.Parsed, Is.Not.Null);
        Assert.That(CliParseProbeCommand.Parsed!.GenerateConfig.IsSet, Is.True);
        Assert.That(CliParseProbeCommand.Parsed.GenerateConfig.Value, Is.EqualTo(".\\config"));
    }

    private static void Parse(string commandLine)
    {
        var services = new ServiceCollection();
        services.AddSingleton<CliParseProbeCommand>();

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp<CliParseProbeCommand>(registrar);

        int exitCode = app.Run(SplitCommandLine(commandLine));

        Assert.That(exitCode, Is.EqualTo(0), "CLI parsing should succeed.");
        Assert.That(CliParseProbeCommand.Parsed, Is.Not.Null, "Settings should be bound.");
    }

    [Test]
    public void Validate_RejectsBothCoverageFormats()
    {
        var result = new AnalyseCommandSettings { GenerateConfig = new FlagValue<string?>(), CoverageCobertura = "a.xml", CoverageClover = "b.xml" }.Validate();

        Assert.That(result.Successful, Is.False);
        Assert.That(result.Message, Is.EqualTo("Only one coverage format can be specified at a time."));
    }

    [Test]
    public void ParseCoverageClover_BindsTheOption()
    {
        Parse(".\\src --coverage-clover .\\clover.xml");

        Assert.That(CliParseProbeCommand.Parsed!.CoverageClover, Is.EqualTo(".\\clover.xml"));
        Assert.That(CliParseProbeCommand.Parsed!.CoverageCobertura, Is.Null);
    }

    private static string[] SplitCommandLine(string commandLine)
        => commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
