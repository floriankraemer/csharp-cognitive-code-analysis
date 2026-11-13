/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.Reporting;
using CognitiveCodeAnalysis.Reporting.Spi;

namespace CognitiveCodeAnalysis.Tests.Reporting;

/// <summary>How <c>-r</c> picks between built-in and custom reporters, and how the configuration declares them.</summary>
public class CustomReporterResolutionTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp()
    {
        _temp = new TempFiles();
        PluginBuilder.Compile(Path.Combine(_temp.tmpDirectory, "plugins"), "Plugin", PluginBuilder.CognitiveReporter);
    }

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private string Output => Path.Combine(_temp.tmpDirectory, "report.out");

    private CognitiveConfiguration ConfigWith(params string[] keys)
    {
        var configuration = new CognitiveConfiguration { ConfigDirectory = _temp.tmpDirectory };
        foreach (string key in keys)
        {
            configuration.CustomReporters.Cognitive[key] =
                new CustomReporterEntry { Type = "Acme.ListingReporter", Assembly = "plugins/Plugin.dll" };
        }

        return configuration;
    }

    private static ReportCoordinator Coordinator() => new([new CsvReport(), new HtmlReport()]);

    [Test]
    public void CustomKey_IsRenderedAndAnnouncedWithItsKey()
    {
        var coordinator = Coordinator();
        ReportGeneratedEventArgs? generated = null;
        coordinator.ReportGenerated += (_, e) => generated = e;

        coordinator.GenerateReport("hotspots", Output, ConfigWith("hotspots"), new CognitiveMetricsCollection());

        Assert.That(File.ReadAllText(Output), Does.StartWith("threshold=0;baseline=False"));
        Assert.That(generated!.ReportType, Is.EqualTo("hotspots"));
        Assert.That(generated.FullPath, Is.EqualTo(Path.GetFullPath(Output)));
    }

    [Test]
    public void CustomKey_IsMatchedExactly()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Coordinator().GenerateReport(
            "HOTSPOTS", Output, ConfigWith("hotspots"), new CognitiveMetricsCollection()));

        Assert.That(ex!.Message, Does.Contain("Unknown report type 'HOTSPOTS'"));
        Assert.That(File.Exists(Output), Is.False);
    }

    [Test]
    public void UnknownType_ListsTheBuiltInsAndTheCustomKeys()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Coordinator().GenerateReport(
            "nope", Output, ConfigWith("zeta", "alpha"), new CognitiveMetricsCollection()));

        Assert.That(ex!.Message, Is.EqualTo(
            "Unknown report type 'nope'. Supported report types: Csv, Html. Custom report types: alpha, zeta."));
    }

    [Test]
    public void BuiltInName_CollidingWithACustomKey_IsAnErrorWhenRequested()
    {
        var ex = Assert.Throws<CustomReporterException>(() => Coordinator().GenerateReport(
            "csv", Output, ConfigWith("CSV"), new CognitiveMetricsCollection()));

        Assert.That(ex!.Message, Does.Contain("collides with the built-in report type 'Csv'").And.EndWith("Rename the key in the configuration."));
        Assert.That(File.Exists(Output), Is.False);
    }

    [Test]
    public void BuiltInName_CollidingWithACustomKey_IsHarmlessWhenAnotherTypeIsRequested()
    {
        Coordinator().GenerateReport("html", Output, ConfigWith("csv", "hotspots"), new CognitiveMetricsCollection());

        Assert.That(File.ReadAllText(Output), Does.Contain("<html"));
    }

    [Test]
    public void Configuration_ReadsCustomReportersAndRemembersTheConfigDirectory()
    {
        string file = _temp.CreateFileWithContent("settings.json", """
            {
              "cognitive": {
                "customReporters": {
                  "cognitive": { "hotspots": { "type": "Acme.ListingReporter", "assembly": "plugins/Plugin.dll" } },
                  "churn": { "list": { "type": "Acme.ClassListing", "assembly": "plugins/Plugin.dll" } }
                }
              }
            }
            """);

        CognitiveConfiguration configuration = ConfigurationLoader.Load(file);

        Assert.That(configuration.CustomReporters.Cognitive["hotspots"].Type, Is.EqualTo("Acme.ListingReporter"));
        Assert.That(configuration.CustomReporters.Cognitive["hotspots"].Assembly, Is.EqualTo("plugins/Plugin.dll"));
        Assert.That(configuration.CustomReporters.Churn["list"].Type, Is.EqualTo("Acme.ClassListing"));
        Assert.That(configuration.ConfigDirectory, Is.EqualTo(_temp.tmpDirectory));
    }

    [Test]
    public void Configuration_WithoutCustomReporters_HasNoneAndNoConfigDirectoryForTheDefaults()
    {
        var defaults = CognitiveConfigurationDefaults.Create();

        Assert.That(defaults.CustomReporters.Cognitive, Is.Empty);
        Assert.That(defaults.CustomReporters.Churn, Is.Empty);
        Assert.That(defaults.ConfigDirectory, Is.Null);
    }

    [Test]
    public void Input_ListsAllMethodsByScoreOrBySorter_WithDeltasAgainstTheBaseline()
    {
        var baselineRow = new CognitiveMetrics("High", "A", "src/A.cs", "void High()", 9) { totalScore = 2, cyclomaticComplexity = 1 };
        CognitiveMetricsCollection currentForBaseline =
        [
            new CognitiveMetrics("High", "A", "src/A.cs", "void High()", 9) { totalScore = 5, cyclomaticComplexity = 4 },
            new CognitiveMetrics("New", "A", "src/A.cs", "void New()", 20) { totalScore = 0.1 },
        ];
        var baseline = BaselineComparer.Compare(
            currentForBaseline,
            BaselineSnapshotFactory.FromMetricsCollection([baselineRow]));
        CognitiveMetricsCollection metrics =
        [
            new CognitiveMetrics("New", "A", "src/A.cs", "void New()", 20) { totalScore = 0.1 },
            new CognitiveMetrics("High", "A", "src/A.cs", "void High()", 9, ifCount: 2) { totalScore = 5, cyclomaticComplexity = 4, lineCoveragePercentage = 50 },
        ];
        var configuration = new CognitiveConfiguration { ScoreThreshold = 2, ShowHalsteadComplexity = true, GroupByClass = true };

        var input = CustomReportInputFactory.Cognitive(metrics, configuration, baseline);

        Assert.That(input.Methods.Select(m => m.MethodName), Is.EqualTo(new[] { "High", "New" }));
        Assert.That(input.HasBaseline, Is.True);
        Assert.That(input.Settings.ScoreThreshold, Is.EqualTo(2));
        Assert.That(input.Settings.ShowHalsteadComplexity, Is.True);
        Assert.That(input.Settings.GroupByClass, Is.True);
        Assert.That(input.Methods[0].Deltas!.TotalScore, Is.EqualTo(3));
        Assert.That(input.Methods[0].Deltas!.CyclomaticComplexity, Is.EqualTo(3));
        Assert.That(input.Methods[0].Deltas!.LineCoverage, Is.Null);
        Assert.That(input.Methods[0].LineCoverage, Is.EqualTo(50));
        Assert.That(input.Methods[0].Ifs.Count, Is.EqualTo(2));
        Assert.That(input.Methods[0].HalsteadVolume, Is.Null);
        Assert.That(input.Methods[1].Deltas, Is.Null, "a method that is not in the baseline has no deltas");

        configuration.Sorter = MetricsSorter.Create("method", "asc");
        Assert.That(CustomReportInputFactory.Cognitive(metrics, configuration, null).Methods.Select(m => m.MethodName),
            Is.EqualTo(new[] { "High", "New" }));
        configuration.Sorter = MetricsSorter.Create("line", "desc");
        var sorted = CustomReportInputFactory.Cognitive(metrics, configuration, null);
        Assert.That(sorted.Methods.Select(m => m.MethodName), Is.EqualTo(new[] { "New", "High" }));
        Assert.That(sorted.HasBaseline, Is.False);
        Assert.That(sorted.Methods[0].Deltas, Is.Null);
    }

    [Test]
    public void Input_FilePathIsRelativeToTheWorkingDirectory()
    {
        string absolute = Path.Combine(Directory.GetCurrentDirectory(), "src", "A.cs");
        CognitiveMetricsCollection metrics = [new CognitiveMetrics("M", "A", absolute, "void M()", 1)];

        var input = CustomReportInputFactory.Cognitive(metrics, new CognitiveConfiguration(), null);

        Assert.That(input.Methods[0].FilePath, Is.EqualTo("src/A.cs"));
    }

    [Test]
    public void SpiRecords_StartEmptyInsteadOfNull()
    {
        var row = new CognitiveMethodRow();
        var churn = new ChurnRow();

        Assert.That(
            new[] { row.FilePath, row.ClassName, row.MethodName, row.MethodSignature, churn.ClassName, churn.FilePath },
            Is.All.EqualTo(string.Empty));
        Assert.That(new CognitiveReportInput().Methods, Is.Empty);
        Assert.That(new ChurnReportInput().Classes, Is.Empty);
    }
}
