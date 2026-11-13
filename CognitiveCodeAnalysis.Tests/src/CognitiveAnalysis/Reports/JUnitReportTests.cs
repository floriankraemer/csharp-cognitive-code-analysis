/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Xml.Linq;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class JUnitReportTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private static CognitiveConfiguration Config(bool onlyViolations = false) => new()
    {
        ScoreThreshold = 5.0,
        ShowOnlyMethodsExceedingThreshold = onlyViolations,
    };

    private static CognitiveMetrics Metric(string cls, string method, double score) =>
        new(method, cls, "src/Ci.cs", "void " + method + "()", 3) { totalScore = score };

    private XDocument Render(CognitiveMetricsCollection collection, CognitiveConfiguration? config = null)
    {
        string path = Path.Combine(_temp.tmpDirectory, "junit.xml");
        new JUnitReport().RenderMetrics(path, collection, config ?? Config());
        return XDocument.Parse(File.ReadAllText(path));
    }

    [Test]
    public void Name_IsTheJUnitReportType()
    {
        Assert.That(new JUnitReport().Name, Is.EqualTo("JUnit"));
    }

    [Test]
    public void Render_EveryMethodIsATestCase_AndViolationsFail()
    {
        var collection = new CognitiveMetricsCollection
        {
            Metric("Svc", "Warn", 6.0),
            Metric("Svc", "Quiet", 1.2),
            Metric("Svc", "Fail", 12.0),
        };

        XDocument doc = Render(collection);

        XElement suites = doc.Root!;
        Assert.That(suites.Name.LocalName, Is.EqualTo("testsuites"));
        Assert.That(suites.Attribute("tests")!.Value, Is.EqualTo("3"));
        Assert.That(suites.Attribute("failures")!.Value, Is.EqualTo("2"));
        Assert.That(suites.Attribute("errors")!.Value, Is.EqualTo("0"));

        XElement suite = suites.Element("testsuite")!;
        Assert.That(suite.Attribute("tests")!.Value, Is.EqualTo("3"));
        Assert.That(suite.Attribute("failures")!.Value, Is.EqualTo("2"));

        var testCases = suite.Elements("testcase").ToList();
        Assert.That(testCases.Select(t => t.Attribute("name")!.Value), Is.EqualTo(new[] { "Warn", "Quiet", "Fail" }));
        Assert.That(testCases.Select(t => t.Attribute("classname")!.Value), Is.All.EqualTo("Svc"));
        Assert.That(testCases.Select(t => t.Attribute("time")!.Value), Is.All.EqualTo("0"));
        Assert.That(testCases[1].Element("failure"), Is.Null);
    }

    [Test]
    public void Render_Failure_CarriesTheScoreMessageAndType()
    {
        XDocument doc = Render(new CognitiveMetricsCollection { Metric("Svc", "Fail", 12.0) });

        XElement failure = doc.Root!.Element("testsuite")!.Element("testcase")!.Element("failure")!;
        Assert.That(failure.Attribute("message")!.Value, Is.EqualTo("Method Fail has cognitive complexity 12.0 (threshold: 5.0)"));
        Assert.That(failure.Attribute("type")!.Value, Is.EqualTo("CognitiveComplexity"));
    }

    [Test]
    public void Render_MethodAtTheThreshold_PassesAsATestCase()
    {
        XDocument doc = Render(new CognitiveMetricsCollection { Metric("Svc", "Edge", 5.0) });

        XElement testCase = doc.Root!.Element("testsuite")!.Element("testcase")!;
        Assert.That(testCase.Element("failure"), Is.Null);
        Assert.That(doc.Root.Attribute("failures")!.Value, Is.EqualTo("0"));
    }

    [Test]
    public void Render_NoMethods_WritesEmptySuites()
    {
        XDocument doc = Render(new CognitiveMetricsCollection());

        Assert.That(doc.Root!.Attribute("tests")!.Value, Is.EqualTo("0"));
        Assert.That(doc.Root.Element("testsuite")!.Elements("testcase"), Is.Empty);
    }

    [Test]
    public void Render_ShowOnlyMethodsExceedingThreshold_ListsOnlyTheFailures()
    {
        var collection = new CognitiveMetricsCollection
        {
            Metric("Svc", "Quiet", 1.2),
            Metric("Svc", "Fail", 12.0),
        };

        XDocument doc = Render(collection, Config(onlyViolations: true));

        Assert.That(doc.Root!.Attribute("tests")!.Value, Is.EqualTo("1"));
        Assert.That(doc.Root.Element("testsuite")!.Elements("testcase").Select(t => t.Attribute("name")!.Value),
            Is.EqualTo(new[] { "Fail" }));
    }

    [Test]
    public void Render_SpecialCharactersInNames_AreEscapedAndParseBack()
    {
        XDocument doc = Render(new CognitiveMetricsCollection { Metric("Ops&Co<T> \"x\"", "Run", 12.0) });

        XElement testCase = doc.Root!.Element("testsuite")!.Element("testcase")!;
        Assert.That(testCase.Attribute("classname")!.Value, Is.EqualTo("Ops&Co<T> \"x\""));
    }

    [Test]
    public void Render_UnwritableDirectory_Throws()
    {
        string blocker = Path.Combine(_temp.tmpDirectory, "file-in-the-way");
        File.WriteAllText(blocker, "x");

        Assert.That(
            () => new JUnitReport().RenderMetrics(
                Path.Combine(blocker, "out.xml"),
                new CognitiveMetricsCollection { Metric("Svc", "Fail", 12.0) },
                Config()),
            Throws.Exception);
    }
}
