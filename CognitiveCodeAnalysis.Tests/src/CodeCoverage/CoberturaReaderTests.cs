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
}
