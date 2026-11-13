/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.Json;
using System.Text.RegularExpressions;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

/// <summary><c>--sort-by</c> must reorder the rows of every report, and leave the default order alone without it.</summary>
public class ReportSortingTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    // Total scores 5, 1, 3 and lines 30, 20, 10: score order and line order are opposite for "alpha" and "beta".
    private static CognitiveMetricsCollection Collection() =>
    [
        new CognitiveMetrics("alpha", "ClassA", "a.cs", "alpha()", 30) { totalScore = 5 },
        new CognitiveMetrics("beta", "ClassA", "a.cs", "beta()", 20) { totalScore = 1 },
        new CognitiveMetrics("gamma", "ClassB", "b.cs", "gamma()", 10) { totalScore = 3 },
    ];

    private static CognitiveConfiguration Config(bool grouped, string? sortBy, string order = "asc") => new()
    {
        ShowOnlyMethodsExceedingThreshold = false,
        GroupByClass = grouped,
        Sorter = sortBy is null ? null : MetricsSorter.Create(sortBy, order),
    };

    private string Render(IReport report, CognitiveConfiguration config, CognitiveBaselineComparison? baseline = null)
    {
        string path = Path.Combine(_temp.tmpDirectory, "out." + report.Name);
        report.RenderMetrics(path, Collection(), config, baseline);
        return File.ReadAllText(path);
    }

    private static string[] Order(string text, string pattern) =>
        Regex.Matches(text, pattern).Select(m => m.Groups[1].Value).ToArray();

    [Test]
    public void Csv_WithoutSort_OrdersByTotalScoreDescending()
        => Assert.That(CsvMethods(Config(false, null)), Is.EqualTo(new[] { "alpha", "gamma", "beta" }));

    [TestCase("line", "asc", new[] { "gamma", "beta", "alpha" })]
    [TestCase("line", "desc", new[] { "alpha", "beta", "gamma" })]
    [TestCase("score", "asc", new[] { "beta", "gamma", "alpha" })]
    [TestCase("method", "desc", new[] { "gamma", "beta", "alpha" })]
    public void Csv_WithSort_FollowsTheSort(string field, string order, string[] expected)
        => Assert.That(CsvMethods(Config(false, field, order)), Is.EqualTo(expected));

    [Test]
    public void Markdown_Ungrouped_WithSort_FollowsTheSort()
        => Assert.That(Order(Render(new MarkdownReport(), Config(false, "line")), @"Method:\*\* `(\w+)\(\)`"), Is.EqualTo(new[] { "gamma", "beta", "alpha" }));

    [Test]
    public void Html_Ungrouped_WithSort_FollowsTheSort()
        => Assert.That(Order(Render(new HtmlReport(), Config(false, "line")), @"Method: (\w+)\(\)"), Is.EqualTo(new[] { "gamma", "beta", "alpha" }));

    [Test]
    public void Markdown_Html_Grouped_WithSort_SortsRowsWithinEachClass()
    {
        // Within ClassA, score ascending puts beta before alpha.
        string markdown = Render(new MarkdownReport(), Config(true, "score"));
        string html = Render(new HtmlReport(), Config(true, "score"));

        Assert.That(MarkdownRows(markdown), Is.EqualTo(new[] { "beta", "alpha", "gamma" }));
        Assert.That(html.IndexOf("L20 beta", StringComparison.Ordinal), Is.LessThan(html.IndexOf("L30 alpha", StringComparison.Ordinal)));
    }

    [Test]
    public void Markdown_Html_Grouped_WithoutSort_KeepScoreDescendingWithinClass()
    {
        string markdown = Render(new MarkdownReport(), Config(true, null));
        string html = Render(new HtmlReport(), Config(true, null));

        Assert.That(MarkdownRows(markdown), Is.EqualTo(new[] { "alpha", "beta", "gamma" }));
        Assert.That(html.IndexOf("L30 alpha", StringComparison.Ordinal), Is.LessThan(html.IndexOf("L20 beta", StringComparison.Ordinal)));
    }

    [Test]
    public void Json_WithoutSort_KeepsTheSnapshotOrder()
        => Assert.That(JsonMethods(Config(false, null), withBaseline: false), Is.EqualTo(new[] { "alpha", "beta", "gamma" }));

    [TestCase("line", "asc", new[] { "gamma", "beta", "alpha" })]
    [TestCase("score", "desc", new[] { "alpha", "gamma", "beta" })]
    public void Json_WithSort_FollowsTheSort(string field, string order, string[] expected)
        => Assert.That(JsonMethods(Config(false, field, order), withBaseline: false), Is.EqualTo(expected));

    [Test]
    public void Json_WithBaseline_WithSort_FollowsTheSort()
        => Assert.That(JsonMethods(Config(false, "line"), withBaseline: true), Is.EqualTo(new[] { "gamma", "beta", "alpha" }));

    [Test]
    public void Json_WithSort_StillLoadsAsBaseline()
    {
        string path = Path.Combine(_temp.tmpDirectory, "baseline.json");
        new JsonReport().RenderMetrics(path, Collection(), Config(false, "line", "desc"));

        var snapshot = BaselineLoader.Load(path);

        Assert.That(snapshot.Methods.Select(m => m.MethodName), Is.EquivalentTo(new[] { "alpha", "beta", "gamma" }));
    }

    [Test]
    public void JUnit_WithoutSort_KeepsTheCollectionOrder()
        => Assert.That(Order(Render(new JUnitReport(), Config(false, null)), @"<testcase [^>]*name=""(\w+)"""), Is.EqualTo(new[] { "alpha", "beta", "gamma" }));

    [Test]
    public void JUnit_WithSort_FollowsTheSort()
        => Assert.That(Order(Render(new JUnitReport(), Config(false, "line")), @"<testcase [^>]*name=""(\w+)"""), Is.EqualTo(new[] { "gamma", "beta", "alpha" }));

    [Test]
    public void Checkstyle_WithoutSort_KeepsTheCollectionOrder()
        => Assert.That(Order(Render(new CheckstyleReport(), Config(false, null)), @"<error line=""(\d+)"""), Is.EqualTo(new[] { "30", "20", "10" }));

    [Test]
    public void Checkstyle_WithSort_FollowsTheSort()
        => Assert.That(Order(Render(new CheckstyleReport(), Config(false, "line")), @"<error line=""(\d+)"""), Is.EqualTo(new[] { "10", "20", "30" }));

    private static string[] MarkdownRows(string markdown) => Order(markdown, @"(?m)^\| L\d+ (alpha|beta|gamma)\b");

    private string[] CsvMethods(CognitiveConfiguration config) =>
        Render(new CsvReport(), config).Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1)
            .Select(line => line.Split(',')[2]).ToArray();

    private string[] JsonMethods(CognitiveConfiguration config, bool withBaseline)
    {
        CognitiveBaselineComparison? comparison = null;
        if (withBaseline)
        {
            comparison = BaselineComparer.Compare(Collection(), BaselineSnapshotFactory.FromMetricsCollection(Collection()));
        }

        string json = Render(new JsonReport(), config, comparison);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("methods").EnumerateArray()
            .Select(m => (m.TryGetProperty("method", out var nested) ? nested : m).GetProperty("methodName").GetString()!)
            .ToArray();
    }
}
