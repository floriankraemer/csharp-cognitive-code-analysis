/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Xml.Linq;

using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

/// <summary>
/// JUnit XML for CI tools such as Jenkins and Maven Surefire. Every method is a test case, and a method above the
/// threshold is a failed one.
/// </summary>
public sealed class JUnitReport : IReport
{
    private const string SuiteName = "Cognitive Complexity";
    private const string FailureType = "CognitiveComplexity";

    public string Name => "JUnit";

    public void RenderMetrics(
        string outputFile,
        CognitiveMetricsCollection metricsCollection,
        CognitiveConfiguration configuration,
        CognitiveBaselineComparison? baselineComparison = null,
        IProgress<AnalysisProgress>? progress = null
    )
    {
        var filtered = ReportMetricsFilter.FilterForReport(metricsCollection, configuration);
        int totalItems = filtered.Count;
        int processedItems = 0;

        ReportProgress.ReportStart(progress, Name, totalItems);

        var testCases = new List<XElement>();
        int failures = 0;
        foreach (var m in ReportMetricsFilter.SortIfRequested(filtered, configuration))
        {
            testCases.Add(TestCaseElement(m, configuration));
            if (CognitiveCiSeverity.IsViolation(m, configuration))
            {
                failures++;
            }

            processedItems++;
            ReportProgress.ReportItem(progress, Name, totalItems, processedItems);
        }

        var suite = new XElement(
            "testsuite",
            SuiteAttributes(totalItems, failures),
            testCases);
        var suites = new XElement(
            "testsuites",
            SuiteAttributes(totalItems, failures),
            suite);

        CognitiveReportFileWriter.Write(outputFile, CognitiveCiXml.Serialize(suites));
        ReportProgress.ReportComplete(progress, Name, totalItems);
    }

    private static IEnumerable<XAttribute> SuiteAttributes(int tests, int failures) =>
    [
        new XAttribute("name", SuiteName),
        new XAttribute("tests", tests),
        new XAttribute("failures", failures),
        new XAttribute("errors", 0),
    ];

    private static XElement TestCaseElement(CognitiveMetrics m, CognitiveConfiguration configuration)
    {
        var testCase = new XElement(
            "testcase",
            new XAttribute("classname", m.ClassName),
            new XAttribute("name", m.MethodName),
            new XAttribute("time", "0"));

        if (CognitiveCiSeverity.IsViolation(m, configuration))
        {
            testCase.Add(new XElement(
                "failure",
                new XAttribute("message", CognitiveCiSeverity.ComplexityMessage(m, configuration)),
                new XAttribute("type", FailureType)));
        }

        return testCase;
    }
}
