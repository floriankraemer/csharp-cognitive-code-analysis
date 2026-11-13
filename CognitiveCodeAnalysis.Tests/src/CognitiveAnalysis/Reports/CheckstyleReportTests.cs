/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text;
using System.Xml.Linq;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class CheckstyleReportTests
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

    private static CognitiveMetrics Metric(string method, string file, int line, double score) =>
        new(method, "Svc", file, "void " + method + "()", line) { totalScore = score };

    private string Render(CognitiveMetricsCollection collection, CognitiveConfiguration? config = null)
    {
        string path = Path.Combine(_temp.tmpDirectory, "checkstyle.xml");
        new CheckstyleReport().RenderMetrics(path, collection, config ?? Config());
        return File.ReadAllText(path);
    }

    [Test]
    public void Name_IsTheCheckstyleReportType()
    {
        Assert.That(new CheckstyleReport().Name, Is.EqualTo("Checkstyle"));
    }

    [Test]
    public void Render_ViolationsAtWarningAndErrorSeverity_ListsEachWithItsSeverity()
    {
        var collection = new CognitiveMetricsCollection
        {
            Metric("Warn", "src/Ci.cs", 4, 6.0),
            Metric("Fail", "src/Ci.cs", 20, 10.0),
        };

        XDocument doc = XDocument.Parse(Render(collection));

        XElement file = doc.Root!.Elements("file").Single();
        Assert.That(file.Attribute("name")!.Value, Is.EqualTo("src/Ci.cs"));
        var errors = file.Elements("error").ToList();
        Assert.That(errors.Select(e => e.Attribute("severity")!.Value), Is.EqualTo(new[] { "warning", "error" }));
        Assert.That(errors.Select(e => e.Attribute("line")!.Value), Is.EqualTo(new[] { "4", "20" }));
        Assert.That(errors[0].Attribute("message")!.Value, Is.EqualTo("Method Warn has cognitive complexity 6.0 (threshold: 5.0)"));
        Assert.That(errors[1].Attribute("message")!.Value, Is.EqualTo("Method Fail has cognitive complexity 10.0 (threshold: 5.0)"));
        Assert.That(errors[0].Attribute("column")!.Value, Is.EqualTo("1"));
        Assert.That(errors[0].Attribute("source")!.Value, Is.EqualTo("CognitiveComplexity"));
    }

    [Test]
    public void Render_MethodsAtOrBelowTheThreshold_AreNotListed()
    {
        var collection = new CognitiveMetricsCollection
        {
            Metric("Edge", "src/Ci.cs", 3, 5.0),
            Metric("Quiet", "src/Ci.cs", 9, 1.2),
        };

        XDocument doc = XDocument.Parse(Render(collection));

        Assert.That(doc.Root!.Elements("file"), Is.Empty);
    }

    [Test]
    public void Render_NoViolations_WritesAnEmptyCheckstyleDocument()
    {
        string xml = Render(new CognitiveMetricsCollection { Metric("Quiet", "src/Ci.cs", 9, 1.2) });

        Assert.That(xml, Is.EqualTo("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<checkstyle version=\"8.0\" />\n"));
    }

    [Test]
    public void Render_MethodOnLineZero_ReportsLineOne()
    {
        XDocument doc = XDocument.Parse(Render(new CognitiveMetricsCollection { Metric("Run", "src/Ci.cs", 0, 7.0) }));

        Assert.That(doc.Root!.Element("file")!.Element("error")!.Attribute("line")!.Value, Is.EqualTo("1"));
    }

    [Test]
    public void Render_FileBelowWorkingDirectory_IsRelativeWithSlashes()
    {
        string inside = Path.Combine(Directory.GetCurrentDirectory(), "src", "Inside.cs");

        XDocument doc = XDocument.Parse(Render(new CognitiveMetricsCollection { Metric("Run", inside, 2, 7.0) }));

        Assert.That(doc.Root!.Element("file")!.Attribute("name")!.Value, Is.EqualTo("src/Inside.cs"));
    }

    [Test]
    public void Render_FileOutsideWorkingDirectory_IsAbsolute()
    {
        XDocument doc = XDocument.Parse(Render(new CognitiveMetricsCollection { Metric("Run", "/tmp/Outside.cs", 2, 7.0) }));

        Assert.That(doc.Root!.Element("file")!.Attribute("name")!.Value, Is.EqualTo("/tmp/Outside.cs"));
    }

    [Test]
    public void Render_FilesAppearInOrderOfTheirFirstViolation_AndGroupTheirErrors()
    {
        var collection = new CognitiveMetricsCollection
        {
            Metric("A", "src/B.cs", 1, 6.0),
            Metric("B", "src/A.cs", 1, 6.0),
            Metric("C", "src/B.cs", 9, 6.0),
        };

        XDocument doc = XDocument.Parse(Render(collection));

        var files = doc.Root!.Elements("file").ToList();
        Assert.That(files.Select(f => f.Attribute("name")!.Value), Is.EqualTo(new[] { "src/B.cs", "src/A.cs" }));
        Assert.That(files[0].Elements("error").Count(), Is.EqualTo(2));
    }

    [Test]
    public void Render_SpecialCharactersInNames_AreEscapedAndParseBack()
    {
        var method = new CognitiveMetrics("Ops&Co<T>", "Ops&Co<T>", "src/a & \"b\" <c>.cs", "void Run()", 3) { totalScore = 12 };

        string xml = Render(new CognitiveMetricsCollection { method });
        XDocument doc = XDocument.Parse(xml);

        Assert.That(xml, Does.Not.Contain("a & \"b\""));
        Assert.That(doc.Root!.Element("file")!.Attribute("name")!.Value, Is.EqualTo("src/a & \"b\" <c>.cs"));
        Assert.That(doc.Root.Element("file")!.Element("error")!.Attribute("severity")!.Value, Is.EqualTo("error"));
    }

    [Test]
    public void Render_WritesUtf8WithoutByteOrderMarkAndWithLineFeeds()
    {
        string path = Path.Combine(_temp.tmpDirectory, "bytes.xml");
        new CheckstyleReport().RenderMetrics(path, new CognitiveMetricsCollection { Metric("Ä", "src/ö.cs", 1, 6.0) }, Config());

        byte[] bytes = File.ReadAllBytes(path);
        string text = Encoding.UTF8.GetString(bytes);

        Assert.That(bytes.Take(3), Is.Not.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.That(text, Does.StartWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<checkstyle"));
        Assert.That(text, Does.Not.Contain("\r"));
        Assert.That(text, Does.Contain("src/ö.cs"));
    }

    [Test]
    public void Render_ShowOnlyMethodsExceedingThreshold_DoesNotChangeTheViolations()
    {
        var collection = new CognitiveMetricsCollection
        {
            Metric("Warn", "src/Ci.cs", 4, 6.0),
            Metric("Quiet", "src/Ci.cs", 9, 1.2),
        };

        XDocument doc = XDocument.Parse(Render(collection, Config(onlyViolations: true)));

        Assert.That(doc.Root!.Element("file")!.Elements("error").Count(), Is.EqualTo(1));
    }

    [Test]
    public void Render_UnwritableDirectory_Throws()
    {
        string blocker = Path.Combine(_temp.tmpDirectory, "file-in-the-way");
        File.WriteAllText(blocker, "x");

        Assert.That(
            () => new CheckstyleReport().RenderMetrics(
                Path.Combine(blocker, "out.xml"),
                new CognitiveMetricsCollection { Metric("Warn", "src/Ci.cs", 4, 6.0) },
                Config()),
            Throws.Exception);
    }
}
