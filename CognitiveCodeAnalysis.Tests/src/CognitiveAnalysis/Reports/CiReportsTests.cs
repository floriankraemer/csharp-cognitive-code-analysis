/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.Json;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class CiReportsTests
{
    private static string WriteSarifAndRead(CognitiveMetricsCollection coll, CognitiveConfiguration config)
    {
        var path = Path.Combine(Path.GetTempPath(), "cog-sarif-" + Guid.NewGuid() + ".json");
        try
        {
            new SarifReport().RenderMetrics(path, coll, config);
            return File.ReadAllText(path);
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
    public void SarifReport_ContainsRuleAndLocation()
    {
        var m = SampleMetric(totalScore: 12.0, line: 10);
        var coll = new CognitiveMetricsCollection { m };
        var config = new CognitiveConfiguration { ScoreThreshold = 5.0, ShowOnlyMethodsExceedingThreshold = false };

        var json = WriteSarifAndRead(coll, config);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        Assert.That(root.GetProperty("version").GetString(), Is.EqualTo("2.1.0"));

        var result = root.GetProperty("runs")[0].GetProperty("results")[0];
        Assert.That(result.GetProperty("ruleId").GetString(), Is.EqualTo("cognitive/method-complexity"));
        Assert.That(result.GetProperty("level").GetString(), Is.EqualTo("warning"));
        Assert.That(
            result.GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("region").GetProperty("startLine").GetInt32(),
            Is.EqualTo(10));
    }

    [Test]
    public void GithubActionsReport_WritesWorkflowLines()
    {
        var m = SampleMetric(totalScore: 3.0, line: 7);
        var coll = new CognitiveMetricsCollection { m };
        var config = new CognitiveConfiguration { ScoreThreshold = 10.0, ShowOnlyMethodsExceedingThreshold = false };

        var path = Path.Combine(Path.GetTempPath(), "cog-gh-" + Guid.NewGuid() + ".txt");
        try
        {
            new GithubActionsReport().RenderMetrics(path, coll, config);
            var text = File.ReadAllText(path);
            Assert.That(text, Does.StartWith("::notice "));
            Assert.That(text, Does.Contain("line=7"));
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
    public void GitlabCodeQualityReport_ContainsSchemaFields()
    {
        var m = SampleMetric(totalScore: 20.0, line: 3);
        var coll = new CognitiveMetricsCollection { m };
        var config = new CognitiveConfiguration { ScoreThreshold = 5.0, ShowOnlyMethodsExceedingThreshold = false };

        var path = Path.Combine(Path.GetTempPath(), "cog-gl-" + Guid.NewGuid() + ".json");
        try
        {
            new GitlabCodeQualityReport().RenderMetrics(path, coll, config);
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var issue = doc.RootElement[0];
            Assert.That(issue.GetProperty("checkName").GetString(), Is.EqualTo("cognitive/method-complexity"));
            Assert.That(issue.GetProperty("severity").GetString(), Is.EqualTo("minor"));
            Assert.That(issue.GetProperty("location").GetProperty("lines").GetProperty("begin").GetInt32(), Is.EqualTo(3));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static CognitiveMetrics SampleMetric(double totalScore, int line)
    {
        var m = new CognitiveMetrics(
            methodName: "Foo",
            className: "C",
            filePath: "/tmp/Sample.cs",
            methodSignature: "void Foo()",
            methodLineNumber: line
        );
        m.totalScore = totalScore;
        return m;
    }
}
