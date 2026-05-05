/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class HtmlReportGroupedTests
{
    [Test]
    public void HtmlReport_GroupByClass_RendersClassSection()
    {
        var m = new CognitiveMetrics(
            methodName: "One",
            className: "Box",
            filePath: "src/Box.cs",
            methodSignature: "void One()",
            methodLineNumber: 3,
            ifCount: 0,
            elseCount: 0,
            loopCount: 0,
            switchCount: 0,
            tryCatchCount: 0,
            returnCount: 0,
            argumentCount: 0,
            linesOfCode: 2,
            nestingLevels: 0,
            cyclomaticComplexity: 1,
            localVariableCount: 0,
            fieldAccessCount: 0,
            propertyAccessCount: 0
        );
        m.totalScore = 1.1;
        var coll = new CognitiveMetricsCollection { m };
        var cfg = new CognitiveConfiguration { GroupByClass = true, ShowOnlyMethodsExceedingThreshold = false };

        var path = Path.Combine(Path.GetTempPath(), "html-grp-" + Guid.NewGuid() + ".html");
        try
        {
            new HtmlReport().RenderMetrics(path, coll, cfg);
            var html = File.ReadAllText(path);
            Assert.That(html, Does.Contain("Class: Box"));
            Assert.That(html, Does.Contain("src/Box.cs"));
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
