/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public sealed class SarifReport : IReport
{
    private const string RuleId = "cognitive/method-complexity";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string Name => "Sarif";

    private static string ToolVersion()
    {
        var assembly = typeof(SarifReport).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        // The SDK appends "+<commit>" build metadata; the stamped release version is the part before it.
        var version = informational?.Split('+')[0];
        return string.IsNullOrWhiteSpace(version) ? assembly.GetName().Version?.ToString() ?? "0.0.0" : version;
    }

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

        var toolVersion = ToolVersion();
        var results = new List<CognitiveSarifResult>();

        foreach (var m in filtered)
        {
            results.Add(new CognitiveSarifResult
            {
                RuleId = RuleId,
                Level = CognitiveCiSeverity.SarifLevel(m, configuration),
                Message = new CognitiveSarifText { Text = CognitiveCiSeverity.BuildMessage(m, configuration, baselineComparison) },
                Locations =
                [
                    new CognitiveSarifLocation
                    {
                        PhysicalLocation = new CognitiveSarifPhysicalLocation
                        {
                            ArtifactLocation = ArtifactLocation(CognitiveCiLocation.DisplayPath(m.FilePath)),
                            Region = new CognitiveSarifRegion
                            {
                                StartLine = CognitiveCiLocation.Line(m),
                                EndLine = CognitiveCiLocation.Line(m),
                            },
                        },
                    },
                ],
            });
            processedItems++;
            ReportProgress.ReportItem(progress, Name, totalItems, processedItems);
        }

        var run = new CognitiveSarifRun
        {
            Tool = new CognitiveSarifTool
            {
                Driver = new CognitiveSarifToolDriver
                {
                    Name = "CognitiveCodeAnalysis",
                    Version = toolVersion,
                    Rules =
                    [
                        new CognitiveSarifRule
                        {
                            Id = RuleId,
                            Name = "Method cognitive complexity",
                            ShortDescription = new CognitiveSarifText { Text = "Cognitive complexity score for a C# method." },
                            FullDescription = new CognitiveSarifText { Text = "Aggregated structural and churn-based complexity metrics per method." },
                        },
                    ],
                },
            },
            OriginalUriBaseIds = new Dictionary<string, CognitiveSarifUriBase>
            {
                [CognitiveCiLocation.SourceRootId] = new CognitiveSarifUriBase
                {
                    Uri = CognitiveCiLocation.WorkingDirectoryUri(Directory.GetCurrentDirectory()),
                },
            },
            Results = results,
        };

        var log = new CognitiveSarifLog
        {
            Schema = "https://raw.githubusercontent.com/oasis-tcs/sarif-spec/master/Schemata/sarif-schema-2.1.0.json",
            Version = "2.1.0",
            Runs = [run],
        };

        var json = JsonSerializer.Serialize(log, JsonOptions);
        CognitiveReportFileWriter.Write(outputFile, json);
        ReportProgress.ReportComplete(progress, Name, totalItems);
    }

    /// <summary>
    /// A relative path is a URI reference against the working directory, which is the source root of the run. An
    /// absolute path is a file URI of its own, since no base applies to it.
    /// </summary>
    private static CognitiveSarifArtifactLocation ArtifactLocation(string displayPath) =>
        CognitiveCiLocation.IsAbsolute(displayPath)
            ? new CognitiveSarifArtifactLocation { Uri = CognitiveCiLocation.FileUri(displayPath) }
            : new CognitiveSarifArtifactLocation
            {
                Uri = CognitiveCiLocation.EncodeUriPath(displayPath),
                UriBaseId = CognitiveCiLocation.SourceRootId,
            };
}

internal sealed class CognitiveSarifLog
{
    [JsonPropertyName("$schema")]
    public required string Schema { get; init; }

    public string Version { get; init; } = "";

    public List<CognitiveSarifRun> Runs { get; init; } = [];
}

internal sealed class CognitiveSarifRun
{
    public CognitiveSarifTool Tool { get; init; } = new();

    public Dictionary<string, CognitiveSarifUriBase> OriginalUriBaseIds { get; init; } = [];

    public List<CognitiveSarifResult> Results { get; init; } = [];
}

internal sealed class CognitiveSarifUriBase
{
    public string Uri { get; init; } = "";
}

internal sealed class CognitiveSarifTool
{
    public CognitiveSarifToolDriver Driver { get; init; } = new();
}

internal sealed class CognitiveSarifToolDriver
{
    public string Name { get; init; } = "";

    public string Version { get; init; } = "";

    public List<CognitiveSarifRule> Rules { get; init; } = [];
}

internal sealed class CognitiveSarifRule
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public CognitiveSarifText? ShortDescription { get; init; }

    public CognitiveSarifText? FullDescription { get; init; }
}

internal sealed class CognitiveSarifText
{
    public string Text { get; init; } = "";
}

internal sealed class CognitiveSarifResult
{
    public string RuleId { get; init; } = "";

    public string Level { get; init; } = "";

    public CognitiveSarifText Message { get; init; } = new();

    public List<CognitiveSarifLocation> Locations { get; init; } = [];
}

internal sealed class CognitiveSarifLocation
{
    public CognitiveSarifPhysicalLocation PhysicalLocation { get; init; } = new();
}

internal sealed class CognitiveSarifPhysicalLocation
{
    public CognitiveSarifArtifactLocation ArtifactLocation { get; init; } = new();

    public CognitiveSarifRegion Region { get; init; } = new();
}

internal sealed class CognitiveSarifArtifactLocation
{
    public string Uri { get; init; } = "";

    public string? UriBaseId { get; init; }
}

internal sealed class CognitiveSarifRegion
{
    public int StartLine { get; init; }

    public int EndLine { get; init; }
}
