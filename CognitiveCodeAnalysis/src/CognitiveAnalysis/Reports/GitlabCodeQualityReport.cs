/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

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
        CognitiveConfiguration configuration
    )
    {
        var filtered = ReportMetricsFilter.FilterForReport(metricsCollection, configuration);
        var issues = filtered.Select(m => new CognitiveGitlabIssue
        {
            Type = "issue",
            CheckName = CheckName,
            Description = CognitiveCiSeverity.BuildMessage(m, configuration),
            Categories = ["Complexity"],
            Severity = CognitiveCiSeverity.GitlabSeverity(m, configuration),
            Fingerprint = ComputeFingerprint(m.FilePath, m.methodLineNumber, m.MethodName),
            Location = new CognitiveGitlabLocation
            {
                Path = CognitiveCiEncoding.NormalizeFilePath(m.FilePath),
                Lines = new CognitiveGitlabLines { Begin = m.methodLineNumber, End = m.methodLineNumber },
            },
        }).ToList();

        var json = JsonSerializer.Serialize(issues, JsonOptions);
        CognitiveReportFileWriter.Write(outputFile, json);
    }

    private static string ComputeFingerprint(string filePath, int line, string methodName)
    {
        byte[] hash;
#if NETSTANDARD2_0
        using (var sha = SHA256.Create())
        {
            hash = sha.ComputeHash(Encoding.UTF8.GetBytes("cognitive|" + filePath + "|" + line + "|" + methodName));
        }

        return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
#else
        hash = SHA256.HashData(Encoding.UTF8.GetBytes("cognitive|" + filePath + "|" + line + "|" + methodName));
        return Convert.ToHexString(hash).ToLowerInvariant();
#endif
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
