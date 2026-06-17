/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysisConsoleApp.Application;
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

    [TestCase("-o", ".\\report.html")]
    [TestCase("--output-file", ".\\report.json")]
    public void ParseOutputOptions_BindsOutputFile(string flag, string outputFile)
    {
        Parse($".\\src -f Html {flag} {outputFile}");

        Assert.That(CliParseProbeCommand.Parsed, Is.Not.Null);
        Assert.That(CliParseProbeCommand.Parsed!.OutputFile, Is.EqualTo(outputFile));
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

    private static string[] SplitCommandLine(string commandLine)
        => commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
