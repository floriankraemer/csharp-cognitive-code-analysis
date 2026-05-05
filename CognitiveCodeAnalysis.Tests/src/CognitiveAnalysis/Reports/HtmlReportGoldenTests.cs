using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class HtmlReportGoldenTests
{
    [Test]
    public void HtmlReport_RendersStableTitleAndMethodRow()
    {
        var m = new CognitiveMetrics(
            methodName: "Alpha",
            className: "Demo",
            filePath: "src/Demo.cs",
            methodSignature: "void Alpha()",
            methodLineNumber: 10,
            ifCount: 1,
            elseCount: 0,
            loopCount: 0,
            switchCount: 0,
            tryCatchCount: 0,
            returnCount: 0,
            argumentCount: 0,
            linesOfCode: 5,
            nestingLevels: 0,
            cyclomaticComplexity: 1,
            localVariableCount: 0,
            fieldAccessCount: 0,
            propertyAccessCount: 0
        );
        m.totalScore = 2.5;

        var coll = new CognitiveMetricsCollection { m };
        var config = new CognitiveConfiguration { GroupByClass = false, ShowOnlyMethodsExceedingThreshold = false };

        var path = Path.Combine(Path.GetTempPath(), "html-golden-" + Guid.NewGuid() + ".html");
        try
        {
            new HtmlReport().RenderMetrics(path, coll, config);
            var html = File.ReadAllText(path);

            Assert.That(html, Does.StartWith("<!DOCTYPE html>"));
            Assert.That(html, Does.Contain("Cognitive Code Analysis Report"));
            Assert.That(html, Does.Contain("Demo"));
            Assert.That(html, Does.Contain("L10"));
            Assert.That(html, Does.Contain("Alpha"));
            Assert.That(html, Does.Contain("2.500"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
