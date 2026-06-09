/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;

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
            Assert.That(html, Does.Contain("report-class-section"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void HtmlReport_GroupByClass_ShowCouplingMetrics_RendersCouplingLine()
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
        coll.SetClassCouplingMetrics(
        [
            new ClassCouplingMetrics
            {
                ClassName = "Box",
                IncomingCoupling = 2,
                OutgoingCoupling = 5,
                Stability = 2.0 / 7.0,
            },
        ]);
        var cfg = new CognitiveConfiguration
        {
            GroupByClass = true,
            ShowOnlyMethodsExceedingThreshold = false,
            ShowCouplingMetrics = true,
        };

        var path = Path.Combine(Path.GetTempPath(), "html-coupling-" + Guid.NewGuid() + ".html");
        try
        {
            new HtmlReport().RenderMetrics(path, coll, cfg);
            var html = File.ReadAllText(path);
            Assert.That(html, Does.Contain("Coupling: In=2, Out=5, Stability=0.286"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void HtmlReport_GroupByClass_SortsByMaxClassScoreThenMethodScoreDescending()
    {
        static CognitiveMetrics Method(
            string methodName,
            string className,
            string filePath,
            double totalScore,
            int line = 1
        ) {
            var m = new CognitiveMetrics(
                methodName: methodName,
                className: className,
                filePath: filePath,
                methodSignature: $"void {methodName}()",
                methodLineNumber: line,
                ifCount: 0,
                elseCount: 0,
                loopCount: 0,
                switchCount: 0,
                tryCatchCount: 0,
                returnCount: 0,
                argumentCount: 0,
                linesOfCode: 1,
                nestingLevels: 0,
                cyclomaticComplexity: 1,
                localVariableCount: 0,
                fieldAccessCount: 0,
                propertyAccessCount: 0
            );
            m.totalScore = totalScore;
            return m;
        }

        var coll = new CognitiveMetricsCollection
        {
            Method("AACold", "HighClass", "src/High.cs", 1.0, line: 2),
            Method("ZZHot", "HighClass", "src/High.cs", 10.0, line: 3),
            Method("Solo", "LowClass", "src/Low.cs", 0.5, line: 4),
        };
        var cfg = new CognitiveConfiguration { GroupByClass = true, ShowOnlyMethodsExceedingThreshold = false };

        var path = Path.Combine(Path.GetTempPath(), "html-sort-" + Guid.NewGuid() + ".html");
        try
        {
            new HtmlReport().RenderMetrics(path, coll, cfg);
            var html = File.ReadAllText(path);

            int highIdx = html.IndexOf("Class: HighClass", StringComparison.Ordinal);
            int lowIdx = html.IndexOf("Class: LowClass", StringComparison.Ordinal);
            Assert.That(highIdx, Is.GreaterThanOrEqualTo(0));
            Assert.That(lowIdx, Is.GreaterThanOrEqualTo(0));
            Assert.That(highIdx, Is.LessThan(lowIdx));

            string highSection = html.Substring(highIdx, lowIdx - highIdx);
            int zzHot = highSection.IndexOf("ZZHot", StringComparison.Ordinal);
            int aaCold = highSection.IndexOf("AACold", StringComparison.Ordinal);
            Assert.That(zzHot, Is.GreaterThanOrEqualTo(0));
            Assert.That(aaCold, Is.GreaterThanOrEqualTo(0));
            Assert.That(zzHot, Is.LessThan(aaCold));
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
