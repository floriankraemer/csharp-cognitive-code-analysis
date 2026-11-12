/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

/// <summary>
/// Golden output for every report with all optional columns enabled, with and without a baseline.
/// Regenerate after an intentional output change with <c>UPDATE_GOLDEN=1</c>.
/// </summary>
public class FullOptionsGoldenTests
{
    public sealed record Format(
        string Name,
        Func<IReport> Create,
        string Extension,
        bool Grouped,
        GoldenNormalization Normalization = GoldenNormalization.None);

    private static readonly Format[] Formats =
    [
        new("csv", () => new CsvReport(), "csv", false),
        new("html.ungrouped", () => new HtmlReport(), "html", false),
        new("html.grouped", () => new HtmlReport(), "html", true),
        new("markdown.ungrouped", () => new MarkdownReport(), "md", false),
        new("markdown.grouped", () => new MarkdownReport(), "md", true),
        new("json", () => new JsonReport(), "json", false, GoldenNormalization.JsonGeneratedAt),
        new("sarif", () => new SarifReport(), "json", false, GoldenNormalization.SarifAssemblyVersion),
        new("gitlab", () => new GitlabCodeQualityReport(), "json", false),
        new("github", () => new GithubActionsReport(), "txt", false),
    ];

    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (Format format in Formats)
        {
            foreach (bool withBaseline in new[] { false, true })
            {
                string variant = withBaseline ? "baseline" : "full";
                yield return new TestCaseData(format, withBaseline).SetName($"{format.Name}_{variant}");
            }
        }
    }

    [TestCaseSource(nameof(Cases))]
    public void Report_MatchesGolden(object formatObject, bool withBaseline)
    {
        var format = (Format)formatObject;
        CognitiveBaselineComparison? baseline = withBaseline ? ReportGoldenFixtures.FullBaselineComparison() : null;
        string fileName = $"{format.Name}.{(withBaseline ? "baseline" : "full")}.{format.Extension}";

        GoldenReportAssert.AssertMatchesGolden(
            format.Create(),
            ReportGoldenFixtures.FullCollection(),
            ReportGoldenFixtures.FullConfig(format.Grouped),
            fileName,
            format.Normalization,
            baseline);
    }
}
