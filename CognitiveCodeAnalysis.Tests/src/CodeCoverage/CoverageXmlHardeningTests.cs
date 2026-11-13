/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

/// <summary>Every reader reads the file and nothing else, and reports a broken file as <see cref="InvalidCoverageFileException"/>.</summary>
public class CoverageXmlHardeningTests
{
    private const string Secret = "TOP-SECRET-CONTENT-OF-A-LOCAL-FILE";

    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private static IEnumerable<TestCaseData> Readers()
    {
        yield return new TestCaseData(new CoberturaReader(), "coverage").SetName("Cobertura");
        yield return new TestCaseData(new VsCoverageReader(), "results").SetName("VisualStudio");
        yield return new TestCaseData(new CloverReader(), "coverage").SetName("Clover");
        yield return new TestCaseData(new AutoDetectCoverageReader(), "coverage").SetName("AutoDetect");
    }

    [TestCaseSource(nameof(Readers))]
    public void ExternalEntity_IsNeverResolved_AndItsContentNeverLeaks(ICoverageReader reader, string root)
    {
        var secret = _temp.CreateFileWithContent("secret.txt", Secret);
        var path = _temp.CreateFileWithContent("xxe.xml", $"""
            <?xml version="1.0"?>
            <!DOCTYPE {root} [<!ENTITY xxe SYSTEM "{new Uri(secret).AbsoluteUri}">]>
            <{root}><project name="&xxe;"/></{root}>
            """);

        var ex = Assert.Throws<InvalidCoverageFileException>(() => reader.ReadCoverage(path).ToList());

        Assert.That(ex!.Message, Does.StartWith($"Invalid coverage file {path}: ").And.Not.Contain(Secret));
    }

    [TestCaseSource(nameof(Readers))]
    public void ExternalDtdAndParameterEntity_AreNeverLoaded(ICoverageReader reader, string root)
    {
        // A parser that loaded the DTD would fail on the file that does not exist, or fetch it.
        var missing = new Uri(Path.Combine(_temp.tmpDirectory, "missing.dtd")).AbsoluteUri;
        var path = _temp.CreateFileWithContent("dtd.xml", $"""
            <?xml version="1.0"?>
            <!DOCTYPE {root} SYSTEM "{missing}" [<!ENTITY % load SYSTEM "{missing}"> %load;]>
            <{root}/>
            """);

        // The document is well-formed, so at most the format check may stop it; a load of the DTD would surface as an IO error.
        try
        {
            reader.ReadCoverage(path).ToList();
        }
        catch (InvalidOperationException)
        {
            // reported as a format error, which is what a document of another format earns
        }
    }

    [TestCaseSource(nameof(Readers))]
    public void EntityBomb_IsRejectedWithoutExpansion(ICoverageReader reader, string root)
    {
        var entities = new System.Text.StringBuilder("<!ENTITY l0 \"lol\">");
        for (var level = 1; level <= 10; level++)
        {
            entities.Append($"<!ENTITY l{level} \"{string.Concat(Enumerable.Repeat($"&l{level - 1};", 10))}\">");
        }

        var path = _temp.CreateFileWithContent("bomb.xml", $"<?xml version=\"1.0\"?><!DOCTYPE {root} [{entities}]><{root} name=\"&l10;\"/>");

        var ex = Assert.Throws<InvalidCoverageFileException>(() => reader.ReadCoverage(path).ToList());

        Assert.That(ex!.Message, Does.StartWith($"Invalid coverage file {path}: "));
    }

    [TestCaseSource(nameof(Readers))]
    public void MalformedXml_IsReportedWithFileAndReason(ICoverageReader reader, string root)
    {
        var path = _temp.CreateFileWithContent("bad.xml", $"<{root}><oops>");

        var ex = Assert.Throws<InvalidCoverageFileException>(() => reader.ReadCoverage(path).ToList());

        Assert.That(ex!.Message, Does.StartWith($"Invalid coverage file {path}: ").And.Length.GreaterThan(path.Length + 25));
    }

    [Test]
    public void CoberturaWithItsUsualDoctype_StillReads()
    {
        var path = _temp.CreateFileWithContent("doctype.xml", """
            <?xml version="1.0"?>
            <!DOCTYPE coverage SYSTEM "http://cobertura.sourceforge.net/xml/coverage-04.dtd">
            <coverage><packages><package name="P"><classes>
              <class name="P.A" filename="A.cs" lines-covered="1" lines-valid="2" branches-covered="0" branches-valid="0"/>
            </classes></package></packages></coverage>
            """);

        var coverage = new AutoDetectCoverageReader().ReadCoverage(path).Single();

        Assert.That(coverage.LineCoveragePercentage, Is.EqualTo(50.0));
    }
}
