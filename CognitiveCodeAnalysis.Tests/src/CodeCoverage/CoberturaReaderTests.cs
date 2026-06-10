/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

public class CoberturaReaderTests
{
    [Test]
    public void ReadCoverage_MinimalCobertura_ReturnsClassEntries()
    {
        var xml = """
            <?xml version="1.0"?>
            <coverage>
              <sources>
                <source>/tmp/proj</source>
              </sources>
              <packages>
                <package name="MyApp">
                  <classes>
                    <class name="Widget" filename="src/Widget.cs" lines-covered="8" lines-valid="10"
                           branches-covered="1" branches-valid="2" complexity="3">
                      <methods>
                        <method name="Run" line-rate="0.8" lines-covered="4" lines-valid="5"
                                branches-covered="0" branches-valid="0" complexity="1">
                          <lines>
                            <line number="42" hits="1"/>
                          </lines>
                        </method>
                      </methods>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var path = Path.Combine(Path.GetTempPath(), "cog-cob-" + Guid.NewGuid() + ".xml");
        try
        {
            File.WriteAllText(path, xml);
            var reader = new CoberturaReader();
            var list = reader.ReadCoverage(path).ToList();

            Assert.That(list.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(list.Any(c => c.FullyQualifiedClassName.Contains("Widget", StringComparison.Ordinal)), Is.True);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void ReadCoverage_MissingFile_Throws()
    {
        var reader = new CoberturaReader();
        Assert.Throws<FileNotFoundException>(() => reader.ReadCoverage(Path.Combine(Path.GetTempPath(), "missing-" + Guid.NewGuid() + ".xml")).ToList());
    }

    [Test]
    public void AutoDetectCoverageReader_SelectsCoberturaReader_ForCoverageRoot()
    {
        var xml = """
            <?xml version="1.0"?>
            <coverage>
              <packages>
                <package name="App">
                  <classes>
                    <class name="Widget" filename="Widget.cs" lines-covered="1" lines-valid="1"
                           branches-covered="0" branches-valid="0" complexity="1">
                      <methods>
                        <method name="Run" line-rate="1" lines-covered="1" lines-valid="1"
                                branches-covered="0" branches-valid="0" complexity="1">
                          <lines>
                            <line number="1" hits="1"/>
                          </lines>
                        </method>
                      </methods>
                    </class>
                  </classes>
                </package>
              </packages>
            </coverage>
            """;
        var path = Path.Combine(Path.GetTempPath(), "cog-autodetect-" + Guid.NewGuid() + ".xml");
        try
        {
            File.WriteAllText(path, xml);
            var reader = new AutoDetectCoverageReader();
            var list = reader.ReadCoverage(path).ToList();

            Assert.That(list, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(list.Any(c => c.FullyQualifiedClassName.Contains("Widget", StringComparison.Ordinal)), Is.True);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void AutoDetectCoverageReader_UnknownRoot_Throws()
    {
        var path = Path.Combine(Path.GetTempPath(), "cog-autodetect-bad-" + Guid.NewGuid() + ".xml");
        try
        {
            File.WriteAllText(path, """<?xml version="1.0"?><unknown/>""");
            var reader = new AutoDetectCoverageReader();
            var ex = Assert.Throws<InvalidOperationException>(() => reader.ReadCoverage(path).ToList());
            Assert.That(ex!.Message, Does.Contain("Unrecognized coverage XML format"));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
