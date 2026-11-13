/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Churn;

namespace CognitiveCodeAnalysis.Tests.Churn;

public class ChurnReportTypesTests
{
    [TestCase("Json", ChurnReportType.Json)]
    [TestCase("json", ChurnReportType.Json)]
    [TestCase("JSON", ChurnReportType.Json)]
    [TestCase("Csv", ChurnReportType.Csv)]
    [TestCase("HTML", ChurnReportType.Html)]
    [TestCase("Markdown", ChurnReportType.Markdown)]
    [TestCase("SvgTreemap", ChurnReportType.SvgTreemap)]
    [TestCase("svgtreemap", ChurnReportType.SvgTreemap)]
    [TestCase("svg-treemap", ChurnReportType.SvgTreemap)]
    [TestCase("SVG", ChurnReportType.SvgTreemap)]
    [TestCase(" csv ", ChurnReportType.Csv)]
    public void TryParse_AcceptsEveryTypeInAnyLetterCase(string value, ChurnReportType expected)
    {
        Assert.That(ChurnReportTypes.TryParse(value, out ChurnReportType type), Is.True);
        Assert.That(type, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("xml")]
    [TestCase("ConsoleText")]
    [TestCase("sarif")]
    [TestCase(null)]
    public void TryParse_RejectsEverythingElse(string? value)
    {
        Assert.That(ChurnReportTypes.TryParse(value, out _), Is.False);
    }
}
