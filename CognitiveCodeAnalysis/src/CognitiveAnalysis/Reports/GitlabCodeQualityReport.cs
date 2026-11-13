/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public sealed class GitlabCodeQualityReport : IReport
{
    private const string CheckName = "cognitive/method-complexity";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Name => "GitlabCodeQuality";

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

        var issues = new List<CognitiveGitlabIssue>();
        foreach (var m in filtered)
        {
            issues.Add(new CognitiveGitlabIssue
            {
                Type = "issue",
                CheckName = CheckName,
                Description = CognitiveCiSeverity.BuildMessage(m, configuration, baselineComparison),
                Categories = ["Complexity"],
                Severity = CognitiveCiSeverity.GitlabSeverity(m, configuration),
                Fingerprint = ComputeFingerprint(CognitiveCiLocation.DisplayPath(m.FilePath), CognitiveCiLocation.Line(m), m.MethodName),
                Location = new CognitiveGitlabLocation
                {
                    Path = CognitiveCiLocation.DisplayPath(m.FilePath),
                    Lines = new CognitiveGitlabLines { Begin = CognitiveCiLocation.Line(m), End = CognitiveCiLocation.Line(m) },
                },
            });
            processedItems++;
            ReportProgress.ReportItem(progress, Name, totalItems, processedItems);
        }

        var json = JsonSerializer.Serialize(issues, JsonOptions);
        CognitiveReportFileWriter.Write(outputFile, json);
        ReportProgress.ReportComplete(progress, Name, totalItems);
    }

    /// <summary>Hashes the displayed path, so the fingerprint is the same on every machine that checks out the repository.</summary>
    private static string ComputeFingerprint(string displayPath, int line, string methodName)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes("cognitive|" + displayPath + "|" + line + "|" + methodName));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

internal sealed class CognitiveGitlabIssue
{
    public string Type { get; init; } = "";

    public string CheckName { get; init; } = "";

    public string Description { get; init; } = "";

    public List<string> Categories { get; init; } = [];

    public string Severity { get; init; } = "";

    public string Fingerprint { get; init; } = "";

    public CognitiveGitlabLocation Location { get; init; } = new();
}

internal sealed class CognitiveGitlabLocation
{
    public string Path { get; init; } = "";

    public CognitiveGitlabLines Lines { get; init; } = new();
}

internal sealed class CognitiveGitlabLines
{
    public int Begin { get; init; }

    public int End { get; init; }
}
