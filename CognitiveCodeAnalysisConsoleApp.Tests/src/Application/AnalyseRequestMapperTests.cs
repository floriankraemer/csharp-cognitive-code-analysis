/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysisConsoleApp.Application;
using CognitiveCodeAnalysisConsoleApp.Commands;

using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Application;

public class AnalyseRequestMapperTests
{
    private static AnalyseCommandSettings Settings(
        string? reportType = null, bool? halstead = null, bool? cyclomatic = null, bool? coupling = null) => new()
    {
        GenerateConfig = new FlagValue<string?>(),
        SourcePath = "src",
        ConfigFile = "cfg.json",
        BaselineFile = "base.json",
        OutputFile = "out.html",
        CoverageCobertura = "cov.xml",
        ReportType = reportType,
        ShowHalstead = halstead,
        ShowCyclomatic = cyclomatic,
        ShowCoupling = coupling,
        Verbose = true,
        GenerateBaseline = true,
        SortBy = "line",
        SortOrder = "desc",
        Debug = true,
    };

    [Test]
    public void FromSettings_CopiesEveryField()
    {
        var request = AnalyseRequestMapper.FromSettings(Settings(reportType: "Html"));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(request.SourcePath, Is.EqualTo("src"));
            Assert.That(request.ConfigFile, Is.EqualTo("cfg.json"));
            Assert.That(request.ReportType, Is.EqualTo("Html"));
            Assert.That(request.BaselineFile, Is.EqualTo("base.json"));
            Assert.That(request.OutputFile, Is.EqualTo("out.html"));
            Assert.That(request.CoverageCobertura, Is.EqualTo("cov.xml"));
            Assert.That(request.Verbose, Is.True);
            Assert.That(request.GenerateBaseline, Is.True);
            Assert.That(request.SortBy, Is.EqualTo("line"));
            Assert.That(request.SortOrder, Is.EqualTo("desc"));
            Assert.That(request.Debug, Is.True);
        }
    }

    [Test]
    public void FromSettings_MissingReportType_DefaultsToConsoleText()
    {
        Assert.That(AnalyseRequestMapper.FromSettings(Settings()).ReportType, Is.EqualTo("ConsoleText"));
    }

    [Test]
    public void FromSettings_NoDisplayFlags_LeavesOverridesNull()
    {
        Assert.That(AnalyseRequestMapper.FromSettings(Settings()).DisplayOverrides, Is.Null);
    }

    [TestCase(true, null, null)]
    [TestCase(null, false, null)]
    [TestCase(null, null, true)]
    [TestCase(true, false, true)]
    public void FromSettings_AnyDisplayFlag_CreatesOverridesPreservingUnsetFlags(bool? halstead, bool? cyclomatic, bool? coupling)
    {
        var overrides = AnalyseRequestMapper.FromSettings(Settings(halstead: halstead, cyclomatic: cyclomatic, coupling: coupling)).DisplayOverrides;

        Assert.That(overrides, Is.Not.Null);
        Assert.That(overrides!.ShowHalstead, Is.EqualTo(halstead));
        Assert.That(overrides.ShowCyclomatic, Is.EqualTo(cyclomatic));
        Assert.That(overrides.ShowCoupling, Is.EqualTo(coupling));
    }
}
