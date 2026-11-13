/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

/// <summary>
/// Report files must not depend on the culture of the machine that writes them.
/// </summary>
public class ReportCultureTests
{
    private CultureInfo _culture = null!;
    private CultureInfo? _defaultCulture;

    [SetUp]
    public void SetUp()
    {
        _culture = CultureInfo.CurrentCulture;
        _defaultCulture = CultureInfo.DefaultThreadCurrentCulture;
    }

    [TearDown]
    public void TearDown()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentCulture = _defaultCulture;
    }

    private static IEnumerable<TestCaseData> Reports()
    {
        yield return new TestCaseData(new Func<IReport>(() => new CsvReport()), GoldenNormalization.None).SetName("csv");
        yield return new TestCaseData(new Func<IReport>(() => new HtmlReport()), GoldenNormalization.None).SetName("html");
        yield return new TestCaseData(new Func<IReport>(() => new MarkdownReport()), GoldenNormalization.None).SetName("markdown");
        yield return new TestCaseData(new Func<IReport>(() => new JsonReport()), GoldenNormalization.JsonGeneratedAt).SetName("json");
        yield return new TestCaseData(new Func<IReport>(() => new SarifReport()), GoldenNormalization.SarifAssemblyVersion).SetName("sarif");
        yield return new TestCaseData(new Func<IReport>(() => new GitlabCodeQualityReport()), GoldenNormalization.None).SetName("gitlab");
        yield return new TestCaseData(new Func<IReport>(() => new GithubActionsReport()), GoldenNormalization.None).SetName("github");
        yield return new TestCaseData(new Func<IReport>(() => new CheckstyleReport()), GoldenNormalization.None).SetName("checkstyle");
        yield return new TestCaseData(new Func<IReport>(() => new JUnitReport()), GoldenNormalization.None).SetName("junit");
    }

    [TestCaseSource(nameof(Reports))]
    public void Report_UnderGermanCulture_IsIdenticalToInvariantOutput(Func<IReport> create, GoldenNormalization normalization)
    {
        string Render(bool withBaseline) => GoldenReportAssert.RenderReport(
            create(),
            ReportGoldenFixtures.FullCollection(),
            ReportGoldenFixtures.FullConfig(groupByClass: true),
            normalization,
            withBaseline ? ReportGoldenFixtures.FullBaselineComparison() : null);

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        string[] invariant = [Render(false), Render(true)];

        var german = new CultureInfo("de-DE");
        CultureInfo.CurrentCulture = german;
        CultureInfo.DefaultThreadCurrentCulture = german;
        string[] underGerman = [Render(false), Render(true)];

        Assert.That(underGerman, Is.EqualTo(invariant));
    }

    [Test]
    public void GermanCulture_WouldHaveChangedTheDecimalSeparator_SoTheTestIsMeaningful()
    {
        Assert.That(1.5.ToString("F3", new CultureInfo("de-DE")), Is.EqualTo("1,500"));
    }
}
