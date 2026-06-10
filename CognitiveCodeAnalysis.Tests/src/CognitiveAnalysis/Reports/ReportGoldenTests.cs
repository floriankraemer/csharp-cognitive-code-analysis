/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

/// <summary>
/// Golden master tests for all core report types.
/// Regenerate fixtures: <c>dotnet test --filter "FullyQualifiedName~RegenerateGoldenFixtures"</c>
/// </summary>
public class ReportGoldenTests
{
    private static readonly GoldenFixtureCase[] Cases =
    [
        new(
            Name: "CsvReport_standard",
            CreateReport: () => new CsvReport(),
            Collection: ReportGoldenFixtures.StandardCollection,
            Config: ReportGoldenFixtures.StandardConfig,
            FixtureFile: "csv.standard.csv"),
        new(
            Name: "HtmlReport_ungrouped",
            CreateReport: () => new HtmlReport(),
            Collection: ReportGoldenFixtures.StandardCollection,
            Config: ReportGoldenFixtures.StandardConfig,
            FixtureFile: "html.ungrouped.html"),
        new(
            Name: "HtmlReport_grouped",
            CreateReport: () => new HtmlReport(),
            Collection: ReportGoldenFixtures.GroupedCollection,
            Config: ReportGoldenFixtures.GroupedConfig,
            FixtureFile: "html.grouped.html"),
        new(
            Name: "MarkdownReport_ungrouped",
            CreateReport: () => new MarkdownReport(),
            Collection: ReportGoldenFixtures.StandardCollection,
            Config: ReportGoldenFixtures.StandardConfig,
            FixtureFile: "markdown.ungrouped.md"),
        new(
            Name: "MarkdownReport_grouped",
            CreateReport: () => new MarkdownReport(),
            Collection: ReportGoldenFixtures.GroupedCollection,
            Config: ReportGoldenFixtures.GroupedConfig,
            FixtureFile: "markdown.grouped.md"),
        new(
            Name: "SarifReport_standard",
            CreateReport: () => new SarifReport(),
            Collection: ReportGoldenFixtures.StandardCollection,
            Config: ReportGoldenFixtures.StandardConfig,
            FixtureFile: "sarif.standard.json",
            Normalization: GoldenNormalization.SarifAssemblyVersion),
        new(
            Name: "GitlabCodeQualityReport_standard",
            CreateReport: () => new GitlabCodeQualityReport(),
            Collection: ReportGoldenFixtures.StandardCollection,
            Config: ReportGoldenFixtures.StandardConfig,
            FixtureFile: "gitlab.standard.json"),
        new(
            Name: "GithubActionsReport_standard",
            CreateReport: () => new GithubActionsReport(),
            Collection: ReportGoldenFixtures.StandardCollection,
            Config: ReportGoldenFixtures.StandardConfig,
            FixtureFile: "github.standard.txt"),
    ];

    public static IEnumerable<TestCaseData> GoldenCaseSource()
    {
        foreach (GoldenFixtureCase testCase in Cases)
        {
            yield return new TestCaseData(testCase).SetName(testCase.Name);
        }
    }

    [TestCaseSource(nameof(GoldenCaseSource))]
    public void Report_MatchesGoldenFixture(GoldenFixtureCase testCase)
    {
        GoldenReportAssert.AssertMatchesGolden(
            testCase.CreateReport(),
            testCase.Collection(),
            testCase.Config(),
            testCase.FixtureFile,
            testCase.Normalization);
    }

    [Test]
    [Explicit("Run manually after intentional report output changes.")]
    public void RegenerateGoldenFixtures()
    {
        foreach (GoldenFixtureCase testCase in Cases)
        {
            string content = GoldenReportAssert.RenderReport(
                testCase.CreateReport(),
                testCase.Collection(),
                testCase.Config(),
                testCase.Normalization);
            GoldenReportAssert.WriteGoldenFile(testCase.FixtureFile, content, testCase.Normalization);
        }
    }

    public sealed record GoldenFixtureCase(
        string Name,
        Func<IReport> CreateReport,
        Func<CognitiveMetricsCollection> Collection,
        Func<CognitiveConfiguration> Config,
        string FixtureFile,
        GoldenNormalization Normalization = GoldenNormalization.None);
}
