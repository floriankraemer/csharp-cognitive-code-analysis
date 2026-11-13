/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysisConsoleApp.Commands;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Commands;

public class ChurnCommandSettingsTests
{
    [Test]
    public void Defaults_AreSinceTheYearTwoThousandAndGit()
    {
        var settings = new ChurnCommandSettings();

        Assert.That(settings.Validate().Successful, Is.True);
        Assert.That(settings.SinceDate, Is.EqualTo(new DateOnly(2000, 1, 1)));
        Assert.That(settings.Vcs, Is.EqualTo("git"));
        Assert.That(settings.ReportTypeValue, Is.Null);
    }

    [TestCase("2024-02-29")]
    [TestCase("1999-12-31")]
    public void Validate_AcceptsRealDates(string since)
    {
        Assert.That(new ChurnCommandSettings { Since = since }.Validate().Successful, Is.True);
    }

    [TestCase("")]
    [TestCase("yesterday")]
    [TestCase("2024-13-01")]
    [TestCase("2023-02-29")]
    [TestCase("2024/01/01")]
    [TestCase("01.01.2024")]
    [TestCase("2024-1-1")]
    [TestCase("2024-01-01 12:00")]
    public void Validate_RejectsAnythingButYyyyMmDd(string since)
    {
        var result = new ChurnCommandSettings { Since = since }.Validate();

        Assert.That(result.Successful, Is.False);
        Assert.That(result.Message, Is.EqualTo($"Invalid --since date '{since}'. Expected format: yyyy-MM-dd."));
    }

    [TestCase("git")]
    [TestCase("GIT")]
    public void Validate_AcceptsGit(string vcs)
    {
        Assert.That(new ChurnCommandSettings { Vcs = vcs }.Validate().Successful, Is.True);
    }

    [TestCase("svn")]
    [TestCase("hg")]
    [TestCase("")]
    public void Validate_RejectsOtherVersionControlSystems(string vcs)
    {
        var result = new ChurnCommandSettings { Vcs = vcs }.Validate();

        Assert.That(result.Successful, Is.False);
        Assert.That(result.Message, Is.EqualTo($"Unknown VCS '{vcs}'. Supported: git."));
    }

    [TestCase("Json")]
    [TestCase("csv")]
    [TestCase("HTML")]
    [TestCase("Markdown")]
    [TestCase("SvgTreemap")]
    public void Validate_AcceptsEveryReportTypeInAnyLetterCase(string type)
    {
        var settings = new ChurnCommandSettings { ReportType = type, OutputFile = "out" };

        Assert.That(settings.Validate().Successful, Is.True);
        Assert.That(settings.ReportTypeValue, Is.Not.Null);
    }

    [TestCase("ConsoleText")]
    [TestCase("Sarif")]
    [TestCase("")]
    public void Validate_LeavesUnknownReportTypesToTheCommand_BecauseTheyMayBeCustomReporters(string type)
    {
        var settings = new ChurnCommandSettings { ReportType = type, OutputFile = "out" };

        Assert.That(settings.Validate().Successful, Is.True);
        Assert.That(settings.ReportTypeValue, Is.Null);
    }

    [Test]
    public void Validate_RequiresReportTypeAndOutputFileTogether()
    {
        var typeOnly = new ChurnCommandSettings { ReportType = "Json" }.Validate();
        var fileOnly = new ChurnCommandSettings { OutputFile = "out.json" }.Validate();

        Assert.That(typeOnly.Successful, Is.False);
        Assert.That(fileOnly.Successful, Is.False);
        Assert.That(typeOnly.Message, Is.EqualTo("--report-type and --output-file must be given together."));
        Assert.That(fileOnly.Message, Is.EqualTo(typeOnly.Message));
    }
}
