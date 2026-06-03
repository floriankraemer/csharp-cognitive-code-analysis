/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.HalsteadAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class CsvReportTests
{
    private static string WriteCsvAndRead(CognitiveMetricsCollection coll, CognitiveConfiguration config)
    {
        var path = Path.Combine(Path.GetTempPath(), "cog-csv-" + Guid.NewGuid() + ".csv");
        try
        {
            new CsvReport().RenderMetrics(path, coll, config);
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
    public void CsvReport_WritesHeaderAndDataRow()
    {
        var m = SampleMetric(totalScore: 12.0, line: 10);
        var coll = new CognitiveMetricsCollection { m };
        var config = new CognitiveConfiguration { ScoreThreshold = 5.0, ShowOnlyMethodsExceedingThreshold = false };

        var csv = WriteCsvAndRead(coll, config);
        var lines = csv.TrimEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        Assert.That(lines.Length, Is.EqualTo(2));
        Assert.That(lines[0], Does.StartWith("FilePath,ClassName,MethodName,MethodSignature,LineNumber,TotalScore"));
        Assert.That(lines[1], Does.Contain("/tmp/Sample.cs"));
        Assert.That(lines[1], Does.Contain(",C,"));
        Assert.That(lines[1], Does.Contain(",Foo,"));
        Assert.That(lines[1], Does.Contain(",10,"));
        Assert.That(lines[1], Does.Contain(",12.000,"));
    }

    [Test]
    public void CsvReport_RespectsThresholdFilter()
    {
        var low = SampleMetric(totalScore: 1.0, line: 1);
        low.MethodName = "Low";
        var high = SampleMetric(totalScore: 10.0, line: 2);
        high.MethodName = "High";
        var coll = new CognitiveMetricsCollection { low, high };
        var config = new CognitiveConfiguration { ScoreThreshold = 5.0, ShowOnlyMethodsExceedingThreshold = true };

        var csv = WriteCsvAndRead(coll, config);
        var lines = csv.TrimEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

        Assert.That(lines.Length, Is.EqualTo(2)); // header + 1 data
        Assert.That(lines[1], Does.Contain("High"));
        Assert.That(lines[1], Does.Not.Contain("Low"));
    }

    [Test]
    public void CsvReport_EscapesSpecialCharacters()
    {
        var m = new CognitiveMetrics(
            methodName: "Bar\"Baz",
            className: "My,Class",
            filePath: "/tmp/Sample.cs",
            methodSignature: "void Bar()",
            methodLineNumber: 5
        );
        m.totalScore = 3.0;
        var coll = new CognitiveMetricsCollection { m };
        var config = new CognitiveConfiguration { ScoreThreshold = 0.0, ShowOnlyMethodsExceedingThreshold = false };

        var csv = WriteCsvAndRead(coll, config);
        var rows = ParseCsv(csv);

        Assert.That(rows.Count, Is.EqualTo(2));
        // columns: 0=FilePath, 1=ClassName, 2=MethodName
        Assert.That(rows[1][1], Is.EqualTo("My,Class"));
        Assert.That(rows[1][2], Is.EqualTo("Bar\"Baz"));
    }

    [Test]
    public void CsvReport_IncludesHalsteadWhenEnabled()
    {
        var m = SampleMetric(totalScore: 4.0, line: 1);
        m.Halstead = new HalsteadMetrics { Volume = 123.45, Difficulty = 2.5, Effort = 308.625 };
        var coll = new CognitiveMetricsCollection { m };
        var configEnabled = new CognitiveConfiguration { ScoreThreshold = 0.0, ShowOnlyMethodsExceedingThreshold = false, ShowHalsteadComplexity = true };
        var configDisabled = new CognitiveConfiguration { ScoreThreshold = 0.0, ShowOnlyMethodsExceedingThreshold = false, ShowHalsteadComplexity = false };

        var csvEnabled = WriteCsvAndRead(coll, configEnabled);
        var headerEnabled = csvEnabled.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0];
        Assert.That(headerEnabled, Does.Contain("HalsteadVolume"));
        Assert.That(headerEnabled, Does.Contain("HalsteadDifficulty"));
        Assert.That(headerEnabled, Does.Contain("HalsteadEffort"));

        var csvDisabled = WriteCsvAndRead(coll, configDisabled);
        var headerDisabled = csvDisabled.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0];
        Assert.That(headerDisabled, Does.Not.Contain("HalsteadVolume"));
    }

    [Test]
    public void CsvReport_IncludesCoverageColumnsWhenPresent()
    {
        var withCov = new CognitiveMetrics(
            methodName: "Cov",
            className: "C",
            filePath: "/tmp/Cov.cs",
            methodSignature: "void Cov()",
            methodLineNumber: 1,
            lineCoveragePercentage: 82.3,
            branchCoveragePercentage: 55.0
        );
        withCov.totalScore = 1.0;
        withCov.churnScore = 0.42;

        var withoutCov = SampleMetric(totalScore: 2.0, line: 2);

        var collWith = new CognitiveMetricsCollection { withCov };
        var collWithout = new CognitiveMetricsCollection { withoutCov };
        var config = new CognitiveConfiguration { ScoreThreshold = 0.0, ShowOnlyMethodsExceedingThreshold = false };

        var csvWith = WriteCsvAndRead(collWith, config);
        var headerWith = csvWith.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0];
        Assert.That(headerWith, Does.Contain("LineCoveragePercent"));
        Assert.That(headerWith, Does.Contain("BranchCoveragePercent"));
        Assert.That(headerWith, Does.Contain("ChurnScore"));

        var csvWithout = WriteCsvAndRead(collWithout, config);
        var headerWithout = csvWithout.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)[0];
        Assert.That(headerWithout, Does.Not.Contain("LineCoveragePercent"));
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

    private static List<List<string>> ParseCsv(string content)
    {
        var result = new List<List<string>>();
        if (string.IsNullOrEmpty(content))
        {
            return result;
        }

        var normalized = content.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = normalized.Split('\n');

        foreach (var line in lines)
        {
            if (string.IsNullOrEmpty(line) && result.Count > 0)
            {
                continue;
            }

            result.Add(ParseCsvLine(line));
        }

        if (result.Count > 0 && result[^1].All(string.IsNullOrEmpty))
        {
            result.RemoveAt(result.Count - 1);
        }

        return result;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuote)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuote = false;
                    }
                }
                else
                {
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuote = true;
            }
            else if (c == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(c);
            }
        }

        fields.Add(field.ToString());
        return fields;
    }
}
