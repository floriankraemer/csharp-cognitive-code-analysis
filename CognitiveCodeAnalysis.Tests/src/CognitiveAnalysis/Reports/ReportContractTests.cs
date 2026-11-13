/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

/// <summary>Behaviour every <see cref="IReport"/> must share: name, progress protocol, output directory creation.</summary>
public class ReportContractTests
{
    public sealed record Contract(string Name, Func<IReport> Create, bool Grouped = false);

    private static readonly Contract[] Reports =
    [
        new("Csv", () => new CsvReport()),
        new("Html", () => new HtmlReport()),
        new("Html", () => new HtmlReport(), Grouped: true),
        new("Markdown", () => new MarkdownReport()),
        new("Markdown", () => new MarkdownReport(), Grouped: true),
        new("Sarif", () => new SarifReport()),
        new("GitlabCodeQuality", () => new GitlabCodeQualityReport()),
        new("GithubActions", () => new GithubActionsReport()),
        new("Checkstyle", () => new CheckstyleReport()),
        new("JUnit", () => new JUnitReport()),
    ];

    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (Contract contract in Reports)
        {
            yield return new TestCaseData(contract).SetName($"{contract.Name}{(contract.Grouped ? "_grouped" : string.Empty)}");
        }
    }

    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private static CognitiveConfiguration Config(bool grouped) => new()
    {
        ScoreThreshold = 5.0,
        ShowOnlyMethodsExceedingThreshold = false,
        GroupByClass = grouped,
    };

    [TestCaseSource(nameof(Cases))]
    public void Name_IsTheDocumentedReportType(Contract contract)
    {
        Assert.That(contract.Create().Name, Is.EqualTo(contract.Name));
    }

    [TestCaseSource(nameof(Cases))]
    public void Progress_StartsAtZero_CountsEveryMethod_AndCompletes(Contract contract)
    {
        var progress = new AnalysisProgressCollector();
        string path = Path.Combine(_temp.tmpDirectory, "out.txt");

        contract.Create().RenderMetrics(path, ReportGoldenFixtures.FullCollection(), Config(contract.Grouped), null, progress);

        Assert.That(
            progress.Reports.Select(r => (r.Phase, r.TotalFiles, r.ProcessedFiles, r.ReportName)),
            Is.EqualTo(new[]
            {
                (AnalysisProgressPhase.WritingReport, 4, 0, contract.Name),
                (AnalysisProgressPhase.WritingReport, 4, 1, contract.Name),
                (AnalysisProgressPhase.WritingReport, 4, 2, contract.Name),
                (AnalysisProgressPhase.WritingReport, 4, 3, contract.Name),
                (AnalysisProgressPhase.WritingReport, 4, 4, contract.Name),
                (AnalysisProgressPhase.ReportCompleted, 4, 4, contract.Name),
            }));
    }

    [TestCaseSource(nameof(Cases))]
    public void Output_CreatesMissingDirectories(Contract contract)
    {
        string path = Path.Combine(_temp.tmpDirectory, "a", "b", "out.txt");

        contract.Create().RenderMetrics(path, ReportGoldenFixtures.FullCollection(), Config(contract.Grouped));

        Assert.That(File.Exists(path), Is.True);
    }

    [TestCaseSource(nameof(Cases))]
    public void Output_OverwritesExistingFile(Contract contract)
    {
        string path = _temp.CreateFileWithContent("out.txt", "OLD CONTENT THAT IS MUCH LONGER THAN NOTHING");

        contract.Create().RenderMetrics(path, ReportGoldenFixtures.FullCollection(), Config(contract.Grouped));

        Assert.That(File.ReadAllText(path), Does.Not.Contain("OLD CONTENT"));
    }

    [Test]
    public void JsonReport_Name_AndProgressWithoutBaseline_ReportsSingleItem()
    {
        var progress = new AnalysisProgressCollector();
        var report = new JsonReport();

        report.RenderMetrics(
            Path.Combine(_temp.tmpDirectory, "x", "snapshot.json"),
            ReportGoldenFixtures.FullCollection(),
            Config(false),
            null,
            progress);

        Assert.That(report.Name, Is.EqualTo("Json"));
        Assert.That(
            progress.Reports.Select(r => (r.Phase, r.TotalFiles, r.ProcessedFiles, r.ReportName)),
            Is.EqualTo(new[]
            {
                (AnalysisProgressPhase.WritingReport, 1, 0, "Json"),
                (AnalysisProgressPhase.WritingReport, 1, 1, "Json"),
                (AnalysisProgressPhase.ReportCompleted, 1, 1, "Json"),
            }));
    }

    [Test]
    public void JsonReport_ProgressWithBaseline_CountsEveryMethod()
    {
        var progress = new AnalysisProgressCollector();

        new JsonReport().RenderMetrics(
            Path.Combine(_temp.tmpDirectory, "snapshot.json"),
            ReportGoldenFixtures.FullCollection(),
            Config(false),
            ReportGoldenFixtures.FullBaselineComparison(),
            progress);

        Assert.That(
            progress.Reports.Select(r => (r.Phase, r.TotalFiles, r.ProcessedFiles)),
            Is.EqualTo(new[]
            {
                (AnalysisProgressPhase.WritingReport, 4, 0),
                (AnalysisProgressPhase.WritingReport, 4, 1),
                (AnalysisProgressPhase.WritingReport, 4, 2),
                (AnalysisProgressPhase.WritingReport, 4, 3),
                (AnalysisProgressPhase.WritingReport, 4, 4),
                (AnalysisProgressPhase.ReportCompleted, 4, 4),
            }));
    }

    [Test]
    public void ShowOnlyMethodsExceedingThreshold_FiltersEveryReportToTheOffenders()
    {
        var config = Config(false);
        config.ShowOnlyMethodsExceedingThreshold = true;

        foreach (Contract contract in Reports.Where(r => !r.Grouped))
        {
            var progress = new AnalysisProgressCollector();
            contract.Create().RenderMetrics(
                Path.Combine(_temp.tmpDirectory, contract.Name + ".out"),
                ReportGoldenFixtures.FullCollection(),
                config,
                null,
                progress);

            Assert.That(progress.Reports[0].TotalFiles, Is.EqualTo(1), contract.Name);
        }
    }
}
