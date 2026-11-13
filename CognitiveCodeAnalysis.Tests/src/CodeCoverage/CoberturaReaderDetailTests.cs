/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

public class CoberturaReaderDetailTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private List<Coverage> Read(string xml)
        => new CoberturaReader().ReadCoverage(_temp.CreateFileWithContent("c.xml", xml)).ToList();

    [Test]
    public void ReadCoverage_ClassAndMethod_MapEveryAttribute()
    {
        var rootDir = Path.GetFullPath(_temp.tmpDirectory);
        var list = Read($"""
            <coverage>
              <sources><source></source><source>{rootDir}</source></sources>
              <packages>
                <package name="MyApp">
                  <classes>
                    <class name="Widget" filename="src/Widget.cs" lines-covered="8" lines-valid="10"
                           branches-covered="1" branches-valid="4" complexity="3">
                      <methods>
                        <method name="Run" lines-covered="4" lines-valid="5" branches-covered="2" branches-valid="2" complexity="7">
                          <lines><line number="42" hits="1"/><line number="43" hits="0"/></lines>
                        </method>
                      </methods>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """);

        Assert.That(list, Has.Count.EqualTo(2));
        var cls = list[0];
        var method = list[1];
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cls.FullyQualifiedClassName, Is.EqualTo("MyApp.Widget"));
            Assert.That(cls.FilePath, Is.EqualTo(Path.Combine(rootDir, "src", "Widget.cs")));
            Assert.That(cls.IsMethodLevel, Is.False);
            Assert.That(cls.MethodLineNumber, Is.EqualTo(0));
            Assert.That(cls.LinesCovered, Is.EqualTo(8));
            Assert.That(cls.LinesTotal, Is.EqualTo(10));
            Assert.That(cls.BranchesCovered, Is.EqualTo(1));
            Assert.That(cls.BranchesTotal, Is.EqualTo(4));
            Assert.That(cls.Complexity, Is.EqualTo(3));
            Assert.That(cls.LineCoveragePercentage, Is.EqualTo(80.0));
            Assert.That(cls.BranchCoveragePercentage, Is.EqualTo(25.0));

            Assert.That(method.FullyQualifiedClassName, Is.EqualTo("MyApp.Widget"));
            Assert.That(method.FilePath, Is.EqualTo(cls.FilePath));
            Assert.That(method.MethodName, Is.EqualTo("Run"));
            Assert.That(method.IsMethodLevel, Is.True);
            Assert.That(method.MethodLineNumber, Is.EqualTo(42));
            Assert.That(method.LinesCovered, Is.EqualTo(4));
            Assert.That(method.LinesTotal, Is.EqualTo(5));
            Assert.That(method.BranchesCovered, Is.EqualTo(2));
            Assert.That(method.BranchesTotal, Is.EqualTo(2));
            Assert.That(method.Complexity, Is.EqualTo(7));
        }
    }

    [Test]
    public void ReadCoverage_EmptyPackageName_UsesBareClassName()
    {
        var list = Read("""<coverage><packages><package name=""><classes><class name="Widget" filename="W.cs"/></classes></package></packages></coverage>""");

        Assert.That(list.Single().FullyQualifiedClassName, Is.EqualTo("Widget"));
    }

    [Test]
    public void ReadCoverage_MissingAttributes_DefaultToEmptyAndZero()
    {
        var list = Read("""<coverage><packages><package><classes><class><methods><method/></methods></class></classes></package></packages></coverage>""");

        Assert.That(list, Has.Count.EqualTo(2));
        foreach (var c in list)
        {
            using (Assert.EnterMultipleScope())
            {
                Assert.That(c.FullyQualifiedClassName, Is.Empty);
                Assert.That(c.FilePath, Is.Empty);
                Assert.That(c.MethodName, Is.Empty);
                Assert.That(c.LinesTotal, Is.EqualTo(0));
                Assert.That(c.LineCoveragePercentage, Is.EqualTo(0.0));
                Assert.That(c.BranchCoveragePercentage, Is.Null);
            }
        }
    }

    [Test]
    public void ReadCoverage_NonNumericCounters_FallBackToZero()
    {
        var list = Read("""<coverage><packages><package name="P"><classes><class name="C" filename="C.cs" lines-covered="abc" lines-valid="" branches-valid="x" complexity="1.5"/></classes></package></packages></coverage>""");

        var c = list.Single();
        Assert.That(c.LinesCovered, Is.EqualTo(0));
        Assert.That(c.LinesTotal, Is.EqualTo(0));
        Assert.That(c.BranchesTotal, Is.EqualTo(0));
        Assert.That(c.Complexity, Is.EqualTo(0));
    }

    [Test]
    public void ReadCoverage_MethodLineNumber_ZeroWhenLinesMissingEmptyOrInvalid()
    {
        var list = Read("""
            <coverage><packages><package name="P"><classes><class name="C" filename="C.cs"><methods>
              <method name="NoLines"/>
              <method name="EmptyLines"><lines/></method>
              <method name="NoNumber"><lines><line hits="1"/></lines></method>
              <method name="BadNumber"><lines><line number="x" hits="1"/></lines></method>
              <method name="Good"><lines><line number="9" hits="1"/></lines></method>
            </methods></class></classes></package></packages></coverage>
            """);

        var byName = list.Where(c => c.IsMethodLevel).ToDictionary(c => c.MethodName, c => c.MethodLineNumber);
        Assert.That(byName["NoLines"], Is.EqualTo(0));
        Assert.That(byName["EmptyLines"], Is.EqualTo(0));
        Assert.That(byName["NoNumber"], Is.EqualTo(0));
        Assert.That(byName["BadNumber"], Is.EqualTo(0));
        Assert.That(byName["Good"], Is.EqualTo(9));
    }

    [Test]
    public void ReadCoverage_RootedFilename_IsKeptAbsolute()
    {
        var absolute = Path.GetFullPath(Path.Combine(_temp.tmpDirectory, "Abs.cs"));
        var list = Read($"""<coverage><sources><source>/ignored</source></sources><packages><package name="P"><classes><class name="C" filename="{absolute}"/></classes></package></packages></coverage>""");

        Assert.That(list.Single().FilePath, Is.EqualTo(absolute));
    }

    [Test]
    public void ReadCoverage_RelativeFilenameWithoutSources_ResolvesAgainstCurrentDirectory()
    {
        var list = Read("""<coverage><packages><package name="P"><classes><class name="C" filename="rel/C.cs"/></classes></package></packages></coverage>""");

        Assert.That(list.Single().FilePath, Is.EqualTo(Path.GetFullPath("rel/C.cs")));
    }

    [Test]
    public void ReadCoverage_NoPackagesOrClasses_ReturnsEmpty()
    {
        Assert.That(Read("<coverage/>"), Is.Empty);
        Assert.That(Read("<coverage><packages><package name=\"P\"/></packages></coverage>"), Is.Empty);
    }

    [Test]
    public void ReadCoverage_MalformedXml_ThrowsInvalidOperationWithInnerException()
    {
        var path = _temp.CreateFileWithContent("bad.xml", "<coverage><oops>");

        var ex = Assert.Throws<InvalidOperationException>(() => new CoberturaReader().ReadCoverage(path).ToList());

        Assert.That(ex!.Message, Does.Contain("Failed to load or parse XML file"));
        Assert.That(ex.InnerException, Is.Not.Null);
    }

    [Test]
    public void ReadCoverage_WrongRootElement_Throws()
    {
        var path = _temp.CreateFileWithContent("root.xml", "<report/>");

        var ex = Assert.Throws<InvalidOperationException>(() => new CoberturaReader().ReadCoverage(path).ToList());

        Assert.That(ex!.Message, Does.Contain("root 'coverage' element not found"));
    }
}
