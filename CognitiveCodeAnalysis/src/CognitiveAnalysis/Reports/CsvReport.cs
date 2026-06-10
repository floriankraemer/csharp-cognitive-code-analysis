/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Text;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public sealed class CsvReport : IReport
{
    public string Name => "Csv";

    public void RenderMetrics(
        string outputFile,
        CognitiveMetricsCollection metricsCollection,
        CognitiveConfiguration configuration,
        CognitiveBaselineComparison? baselineComparison = null,
        IProgress<AnalysisProgress>? progress = null
    )
    {
        var filtered = ReportMetricsFilter.FilterForReport(metricsCollection, configuration);
        bool hasCoverageData = filtered.HasCoverageData();
        bool includeDeltas = baselineComparison != null;
        int totalItems = filtered.Count;
        int processedItems = 0;

        ReportProgress.ReportStart(progress, Name, totalItems);

        var sb = new StringBuilder();
        var headers = BuildHeaders(hasCoverageData, configuration, includeDeltas);
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsvField)));

        foreach (var m in filtered.OrderByDescending(m => m.totalScore))
        {
            MethodMetricsComparison? comparison = null;
            baselineComparison?.TryGetMethodComparison(m, out comparison);
            var row = BuildRow(m, hasCoverageData, configuration, comparison, includeDeltas);
            sb.AppendLine(string.Join(",", row.Select(EscapeCsvField)));
            processedItems++;
            ReportProgress.ReportItem(progress, Name, totalItems, processedItems);
        }

        CognitiveReportFileWriter.Write(outputFile, sb.ToString());
        ReportProgress.ReportComplete(progress, Name, totalItems);
    }

    private static List<string> BuildHeaders(bool hasCoverageData, CognitiveConfiguration configuration, bool includeDeltas)
    {
        var headers = new List<string>
        {
            "FilePath",
            "ClassName",
            "MethodName",
            "MethodSignature",
            "LineNumber",
        };

        AddMetricHeaders(headers, "TotalScore", includeDeltas);
        AddMetricHeaders(headers, "LinesOfCode", includeDeltas);
        AddMetricHeaders(headers, "IfCount", includeDeltas);
        AddMetricHeaders(headers, "IfScore", includeDeltas);
        AddMetricHeaders(headers, "ArgumentCount", includeDeltas);
        AddMetricHeaders(headers, "ArgumentScore", includeDeltas);
        AddMetricHeaders(headers, "NestingLevels", includeDeltas);
        AddMetricHeaders(headers, "NestingScore", includeDeltas);
        AddMetricHeaders(headers, "ReturnCount", includeDeltas);
        AddMetricHeaders(headers, "ReturnScore", includeDeltas);
        AddMetricHeaders(headers, "LocalVariableCount", includeDeltas);
        AddMetricHeaders(headers, "LocalVariableScore", includeDeltas);
        AddMetricHeaders(headers, "FieldAccessCount", includeDeltas);
        AddMetricHeaders(headers, "FieldAccessScore", includeDeltas);
        AddMetricHeaders(headers, "PropertyAccessCount", includeDeltas);
        AddMetricHeaders(headers, "PropertyAccessScore", includeDeltas);

        if (configuration.ShowHalsteadComplexity)
        {
            AddMetricHeaders(headers, "HalsteadVolume", includeDeltas);
            AddMetricHeaders(headers, "HalsteadDifficulty", includeDeltas);
            AddMetricHeaders(headers, "HalsteadEffort", includeDeltas);
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            AddMetricHeaders(headers, "CyclomaticComplexity", includeDeltas);
        }

        if (hasCoverageData)
        {
            AddMetricHeaders(headers, "LineCoveragePercent", includeDeltas);
            AddMetricHeaders(headers, "BranchCoveragePercent", includeDeltas);
            AddMetricHeaders(headers, "ChurnScore", includeDeltas);
        }

        return headers;
    }

    private static void AddMetricHeaders(List<string> headers, string name, bool includeDeltas)
    {
        headers.Add(name);
        if (includeDeltas)
        {
            headers.Add(name + "Delta");
        }
    }

    private static List<string> BuildRow(
        CognitiveMetrics m,
        bool hasCoverageData,
        CognitiveConfiguration configuration,
        MethodMetricsComparison? comparison,
        bool includeDeltas
    )
    {
        var row = new List<string>
        {
            m.FilePath,
            m.ClassName,
            m.MethodName,
            m.methodSignature,
            m.methodLineNumber.ToString(CultureInfo.InvariantCulture),
        };

        AddMetricValue(row, m.totalScore.ToString("F3", CultureInfo.InvariantCulture), comparison?.TotalScore, "F3", includeDeltas);
        AddMetricValue(row, m.linesOfCode.ToString(CultureInfo.InvariantCulture), comparison?.LinesOfCode, "F0", includeDeltas);
        AddMetricValue(row, m.ifCount.ToString(CultureInfo.InvariantCulture), comparison?.IfCount, "F0", includeDeltas);
        AddMetricValue(row, m.ifScore.ToString("F3", CultureInfo.InvariantCulture), comparison?.IfScore, "F3", includeDeltas);
        AddMetricValue(row, m.argumentCount.ToString(CultureInfo.InvariantCulture), comparison?.ArgumentCount, "F0", includeDeltas);
        AddMetricValue(row, m.argumentScore.ToString("F3", CultureInfo.InvariantCulture), comparison?.ArgumentScore, "F3", includeDeltas);
        AddMetricValue(row, m.nestingLevels.ToString(CultureInfo.InvariantCulture), comparison?.NestingLevels, "F0", includeDeltas);
        AddMetricValue(row, m.nestingScore.ToString("F3", CultureInfo.InvariantCulture), comparison?.NestingScore, "F3", includeDeltas);
        AddMetricValue(row, m.returnCount.ToString(CultureInfo.InvariantCulture), comparison?.ReturnCount, "F0", includeDeltas);
        AddMetricValue(row, m.returnScore.ToString("F3", CultureInfo.InvariantCulture), comparison?.ReturnScore, "F3", includeDeltas);
        AddMetricValue(row, m.localVariableCount.ToString(CultureInfo.InvariantCulture), comparison?.LocalVariableCount, "F0", includeDeltas);
        AddMetricValue(row, m.localVariableScore.ToString("F3", CultureInfo.InvariantCulture), comparison?.LocalVariableScore, "F3", includeDeltas);
        AddMetricValue(row, m.fieldAccessCount.ToString(CultureInfo.InvariantCulture), comparison?.FieldAccessCount, "F0", includeDeltas);
        AddMetricValue(row, m.fieldAccessScore.ToString("F3", CultureInfo.InvariantCulture), comparison?.FieldAccessScore, "F3", includeDeltas);
        AddMetricValue(row, m.propertyAccessCount.ToString(CultureInfo.InvariantCulture), comparison?.PropertyAccessCount, "F0", includeDeltas);
        AddMetricValue(row, m.propertyAccessScore.ToString("F3", CultureInfo.InvariantCulture), comparison?.PropertyAccessScore, "F3", includeDeltas);

        if (configuration.ShowHalsteadComplexity)
        {
            AddMetricValue(row, FormatHalstead(m.Halstead?.Volume), comparison?.HalsteadVolume, "F2", includeDeltas);
            AddMetricValue(row, FormatHalstead(m.Halstead?.Difficulty), comparison?.HalsteadDifficulty, "F2", includeDeltas);
            AddMetricValue(row, FormatHalstead(m.Halstead?.Effort), comparison?.HalsteadEffort, "F2", includeDeltas);
        }

        if (configuration.ShowCyclomaticComplexity)
        {
            AddMetricValue(
                row,
                m.cyclomaticComplexity.ToString("F1", CultureInfo.InvariantCulture),
                comparison?.CyclomaticComplexity,
                "F1",
                includeDeltas);
        }

        if (hasCoverageData)
        {
            AddMetricValue(row, FormatCoverage(m.lineCoveragePercentage), comparison?.LineCoveragePercentage, "F1", includeDeltas);
            AddMetricValue(row, FormatCoverage(m.branchCoveragePercentage), comparison?.BranchCoveragePercentage, "F1", includeDeltas);
            AddMetricValue(row, FormatChurn(m.churnScore), comparison?.ChurnScore, "F3", includeDeltas);
        }

        return row;
    }

    private static void AddMetricValue(
        List<string> row,
        string value,
        MetricDelta? delta,
        string deltaFormat,
        bool includeDeltas
    )
    {
        row.Add(value);
        if (includeDeltas)
        {
            row.Add(CognitiveReportDeltaFormatter.FormatCsvDelta(delta, deltaFormat));
        }
    }

    private static string FormatHalstead(double? value)
        => value.HasValue ? value.Value.ToString("F2", CultureInfo.InvariantCulture) : "";

    private static string FormatCoverage(double? value)
        => value.HasValue ? value.Value.ToString("F1", CultureInfo.InvariantCulture) : "";

    private static string FormatChurn(double? value)
        => value.HasValue ? value.Value.ToString("F3", CultureInfo.InvariantCulture) : "";

    private static string EscapeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value))
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
