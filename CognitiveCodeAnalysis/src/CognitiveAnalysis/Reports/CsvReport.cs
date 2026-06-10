/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Text;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public sealed class CsvReport : IReport
{
    public string Name => "Csv";

    public void RenderMetrics(
        string outputFile,
        CognitiveMetricsCollection metricsCollection,
        CognitiveConfiguration configuration
    )
    {
        var filtered = ReportMetricsFilter.FilterForReport(metricsCollection, configuration);
        bool hasCoverageData = filtered.HasCoverageData();

        var sb = new StringBuilder();
        var headers = BuildHeaders(hasCoverageData, configuration);
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsvField)));

        foreach (var m in filtered.OrderByDescending(m => m.totalScore))
        {
            var row = BuildRow(m, hasCoverageData, configuration);
            sb.AppendLine(string.Join(",", row.Select(EscapeCsvField)));
        }

        CognitiveReportFileWriter.Write(outputFile, sb.ToString());
    }

    private static List<string> BuildHeaders(bool hasCoverageData, CognitiveConfiguration configuration)
    {
        var headers = new List<string>
        {
            "FilePath",
            "ClassName",
            "MethodName",
            "MethodSignature",
            "LineNumber",
            "TotalScore",
            "LinesOfCode",
            "IfCount",
            "IfScore",
            "ElseCount",
            "ElseScore",
            "LoopCount",
            "LoopScore",
            "SwitchCount",
            "SwitchScore",
            "TryCatchCount",
            "TryCatchScore",
            "ArgumentCount",
            "ArgumentScore",
            "NestingLevels",
            "NestingScore",
            "ReturnCount",
            "ReturnScore",
            "LocalVariableCount",
            "LocalVariableScore",
            "FieldAccessCount",
            "FieldAccessScore",
            "PropertyAccessCount",
            "PropertyAccessScore",
        };

        if (configuration.ShowHalsteadComplexity)
        {
            headers.Add("HalsteadVolume");
            headers.Add("HalsteadDifficulty");
            headers.Add("HalsteadEffort");
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            headers.Add("CyclomaticComplexity");
        }

        if (hasCoverageData)
        {
            headers.Add("LineCoveragePercent");
            headers.Add("BranchCoveragePercent");
            headers.Add("ChurnScore");
        }

        return headers;
    }

    private static List<string> BuildRow(
        CognitiveMetrics m,
        bool hasCoverageData,
        CognitiveConfiguration configuration
    )
    {
        var row = new List<string>
        {
            m.FilePath,
            m.ClassName,
            m.MethodName,
            m.methodSignature,
            m.methodLineNumber.ToString(CultureInfo.InvariantCulture),
            m.totalScore.ToString("F3", CultureInfo.InvariantCulture),
            m.linesOfCode.ToString(CultureInfo.InvariantCulture),
            m.ifCount.ToString(CultureInfo.InvariantCulture),
            m.ifScore.ToString("F3", CultureInfo.InvariantCulture),
            m.elseCount.ToString(CultureInfo.InvariantCulture),
            m.elseScore.ToString("F3", CultureInfo.InvariantCulture),
            m.loopCount.ToString(CultureInfo.InvariantCulture),
            m.loopScore.ToString("F3", CultureInfo.InvariantCulture),
            m.switchCount.ToString(CultureInfo.InvariantCulture),
            m.switchScore.ToString("F3", CultureInfo.InvariantCulture),
            m.tryCatchCount.ToString(CultureInfo.InvariantCulture),
            m.tryCatchScore.ToString("F3", CultureInfo.InvariantCulture),
            m.argumentCount.ToString(CultureInfo.InvariantCulture),
            m.argumentScore.ToString("F3", CultureInfo.InvariantCulture),
            m.nestingLevels.ToString(CultureInfo.InvariantCulture),
            m.nestingScore.ToString("F3", CultureInfo.InvariantCulture),
            m.returnCount.ToString(CultureInfo.InvariantCulture),
            m.returnScore.ToString("F3", CultureInfo.InvariantCulture),
            m.localVariableCount.ToString(CultureInfo.InvariantCulture),
            m.localVariableScore.ToString("F3", CultureInfo.InvariantCulture),
            m.fieldAccessCount.ToString(CultureInfo.InvariantCulture),
            m.fieldAccessScore.ToString("F3", CultureInfo.InvariantCulture),
            m.propertyAccessCount.ToString(CultureInfo.InvariantCulture),
            m.propertyAccessScore.ToString("F3", CultureInfo.InvariantCulture),
        };

        if (configuration.ShowHalsteadComplexity)
        {
            row.Add(FormatHalstead(m.Halstead?.Volume));
            row.Add(FormatHalstead(m.Halstead?.Difficulty));
            row.Add(FormatHalstead(m.Halstead?.Effort));
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            row.Add(m.cyclomaticComplexity.ToString("F1", CultureInfo.InvariantCulture));
        }

        if (hasCoverageData)
        {
            row.Add(FormatCoverage(m.lineCoveragePercentage));
            row.Add(FormatCoverage(m.branchCoveragePercentage));
            row.Add(FormatChurn(m.churnScore));
        }

        return row;
    }

    private static string FormatHalstead(double? value)
        => value.HasValue ? value.Value.ToString("F2", CultureInfo.InvariantCulture) : "";

    private static string FormatCoverage(double? value)
        => value.HasValue ? value.Value.ToString("F1", CultureInfo.InvariantCulture) : "";

    private static string FormatChurn(double? value)
        => value.HasValue ? value.Value.ToString("F3", CultureInfo.InvariantCulture) : "";

    private static string EscapeCsvField(string? value)
    {
        if (value is null or { Length: 0 })
        {
            return "";
        }

        bool needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\r') || value.Contains('\n');
        if (needsQuoting)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }
}
