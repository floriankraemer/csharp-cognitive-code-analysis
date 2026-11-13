/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

public class VsCoverageReaderDetailTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private List<Coverage> Read(string xml)
        => new VsCoverageReader().ReadCoverage(_temp.CreateFileWithContent("vs.xml", xml)).ToList();

    [Test]
    public void ReadCoverage_RangesAcrossModules_AreMergedPerFileWithCoveredWinning()
    {
        var list = Read("""
            <results>
              <module>
                <functions><range source_id="1" start_line="1" end_line="3" covered="no"/></functions>
                <source_files><source_file id="1" path="/src/A.cs"/></source_files>
              </module>
              <module>
                <functions>
                  <range source_id="9" start_line="2" end_line="2" covered="YES"/>
                  <range source_id="9" start_line="3" covered="no"/>
                </functions>
                <source_files><source_file id="9" path="/src/A.cs"/></source_files>
              </module>
            </results>
            """);

        var a = list.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(a.FilePath, Is.EqualTo("/src/A.cs"));
            Assert.That(a.LinesTotal, Is.EqualTo(3));
            Assert.That(a.LinesCovered, Is.EqualTo(1));
            Assert.That(a.BranchesTotal, Is.EqualTo(0));
            Assert.That(a.IsMethodLevel, Is.False);
            Assert.That(a.FullyQualifiedClassName, Is.Empty);
        }
    }

    [Test]
    public void ReadCoverage_PathsAreCaseInsensitiveKeys()
    {
        var list = Read("""
            <results><module>
              <range source_id="1" start_line="1" covered="yes"/>
              <range source_id="2" start_line="2" covered="yes"/>
              <source_file id="1" path="/src/A.cs"/>
              <source_file id="2" path="/SRC/a.cs"/>
            </module></results>
            """);

        Assert.That(list.Single().LinesTotal, Is.EqualTo(2));
    }

    [Test]
    public void ReadCoverage_IncompleteOrInvalidRanges_AreSkipped()
    {
        var list = Read("""
            <results><module>
              <range start_line="1" covered="yes"/>
              <range source_id="1" covered="yes"/>
              <range source_id="1" start_line="1"/>
              <range source_id="1" start_line="abc" covered="yes"/>
              <range source_id="1" start_line="7" covered="yes"/>
              <range source_id="unknown" start_line="8" covered="yes"/>
              <source_file id="1" path="/src/A.cs"/>
              <source_file id="2"/>
              <source_file path="/src/none.cs"/>
            </module></results>
            """);

        var a = list.Single();
        Assert.That(a.LinesTotal, Is.EqualTo(1));
        Assert.That(a.LinesCovered, Is.EqualTo(1));
    }

    [TestCase("abc")]
    [TestCase("")]
    [TestCase("1.5")]
    public void ReadCoverage_UnparsableEndLine_KeepsTheRangeAsASingleLine(string endLine)
    {
        var list = Read($$"""
            <results><module>
              <range source_id="1" start_line="5" end_line="{{endLine}}" covered="yes"/>
              <range source_id="1" start_line="6" end_line="{{endLine}}" covered="no"/>
              <source_file id="1" path="/src/A.cs"/>
            </module></results>
            """);

        var a = list.Single();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(a.LinesTotal, Is.EqualTo(2));
            Assert.That(a.LinesCovered, Is.EqualTo(1));
        }
    }

    [Test]
    public void ReadCoverage_EndLineBeforeStartLine_KeepsTheRangeAsASingleLine()
    {
        var list = Read("""
            <results><module>
              <range source_id="1" start_line="5" end_line="2" covered="yes"/>
              <source_file id="1" path="/src/A.cs"/>
            </module></results>
            """);

        Assert.That(list.Single().LinesTotal, Is.EqualTo(1));
    }

    [Test]
    public void ReadCoverage_ValidEndLine_StillSpansTheWholeRange()
    {
        var list = Read("""
            <results><module>
              <range source_id="1" start_line="5" end_line="8" covered="yes"/>
              <source_file id="1" path="/src/A.cs"/>
            </module></results>
            """);

        var a = list.Single();
        Assert.That((a.LinesTotal, a.LinesCovered), Is.EqualTo((4, 4)));
    }

    [Test]
    public void ReadCoverage_EmptyModuleAndNoRanges_ReturnsEmpty()
    {
        Assert.That(Read("<results><module/></results>"), Is.Empty);
        Assert.That(Read("<results/>"), Is.Empty);
        Assert.That(Read("<results><module><source_file id=\"1\" path=\"/a.cs\"/></module></results>"), Is.Empty);
    }

    [Test]
    public void ReadCoverage_RelativePath_IsResolvedAgainstCurrentDirectory()
    {
        var list = Read("""<results><module><range source_id="1" start_line="1" covered="yes"/><source_file id="1" path="rel/A.cs"/></module></results>""");

        Assert.That(list.Single().FilePath, Is.EqualTo(Path.GetFullPath("rel/A.cs")));
    }

    [Test]
    public void ReadCoverage_EmptyPath_YieldsEmptyFilePath()
    {
        var list = Read("""<results><module><range source_id="1" start_line="1" covered="yes"/><source_file id="1" path=""/></module></results>""");

        Assert.That(list.Single().FilePath, Is.Empty);
    }

    [Test]
    public void ReadCoverage_MalformedXml_ThrowsInvalidOperationWithInnerException()
    {
        var path = _temp.CreateFileWithContent("bad.xml", "<results><module>");

        var ex = Assert.Throws<InvalidCoverageFileException>(() => new VsCoverageReader().ReadCoverage(path));

        Assert.That(ex!.Message, Does.StartWith($"Invalid coverage file {path}: "));
        Assert.That(ex.InnerException, Is.Not.Null);
    }

    [Test]
    public void ReadCoverage_DocumentWithoutElements_Throws()
    {
        var path = _temp.CreateFileWithContent("empty.xml", "<!-- nothing -->");

        // An XML document without a root element is rejected by the parser; it is reported as a parse failure.
        Assert.Throws<InvalidCoverageFileException>(() => new VsCoverageReader().ReadCoverage(path));
    }

    [Test]
    public void ReadCoverage_WrongRoot_NamesTheRootInTheMessage()
    {
        var path = _temp.CreateFileWithContent("wrong.xml", "<coverage/>");

        var ex = Assert.Throws<InvalidOperationException>(() => new VsCoverageReader().ReadCoverage(path));

        Assert.That(ex!.Message, Does.Contain("found 'coverage'"));
    }

    [Test]
    public void AutoDetect_MissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(
            () => new AutoDetectCoverageReader().ReadCoverage(Path.Combine(_temp.tmpDirectory, "none.xml")));
    }

    [Test]
    public void AutoDetect_FileWithoutElements_Throws()
    {
        var path = _temp.CreateFileWithContent("noelem.xml", "");

        Assert.Throws<InvalidCoverageFileException>(() => new AutoDetectCoverageReader().ReadCoverage(path));
    }
}
