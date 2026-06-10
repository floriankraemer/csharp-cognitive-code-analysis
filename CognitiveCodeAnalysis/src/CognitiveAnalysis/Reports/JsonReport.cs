/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.Json;
using System.Text.Json.Serialization;

using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

public sealed class JsonReport : IReport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    public string Name => "Json";

    public void RenderMetrics(
        string outputFile,
        CognitiveMetricsCollection metricsCollection,
        CognitiveConfiguration configuration,
        CognitiveBaselineComparison? baselineComparison = null,
        IProgress<AnalysisProgress>? progress = null
    )
    {
        var snapshot = BaselineSnapshotFactory.FromMetricsCollection(metricsCollection);

        if (baselineComparison == null)
        {
            ReportProgress.ReportStart(progress, Name, 1);
            CognitiveReportFileWriter.Write(outputFile, BaselineLoader.Serialize(snapshot));
            ReportProgress.ReportItem(progress, Name, 1, 1);
            ReportProgress.ReportComplete(progress, Name, 1);
            return;
        }

        int totalItems = metricsCollection.Count;
        int processedItems = 0;

        ReportProgress.ReportStart(progress, Name, totalItems);

        var methods = new List<JsonReportMethodWithDeltas>();
        foreach (var metrics in metricsCollection)
        {
            baselineComparison.TryGetMethodComparison(metrics, out MethodMetricsComparison? comparison);
            methods.Add(new JsonReportMethodWithDeltas
            {
                Method = ToMethodSnapshot(metrics),
                Deltas = comparison is { HasBaseline: true } ? BuildMethodDeltas(comparison) : null,
            });
            processedItems++;
            ReportProgress.ReportItem(progress, Name, totalItems, processedItems);
        }

        var output = new JsonReportWithDeltas
        {
            SchemaVersion = snapshot.SchemaVersion,
            GeneratedAt = snapshot.GeneratedAt,
            Methods = methods,
            ClassCoupling = BuildClassCouplingWithDeltas(snapshot, baselineComparison),
        };

        var json = JsonSerializer.Serialize(output, JsonOptions);
        CognitiveReportFileWriter.Write(outputFile, json);
        ReportProgress.ReportComplete(progress, Name, totalItems);
    }

    private static List<JsonReportClassCouplingWithDeltas> BuildClassCouplingWithDeltas(
        CognitiveBaselineSnapshot snapshot,
        CognitiveBaselineComparison baselineComparison
    ) =>
        snapshot.ClassCoupling.Select(coupling =>
        {
            baselineComparison.TryGetClassCouplingComparison(coupling.ClassName, out ClassCouplingComparison? comparison);
            return new JsonReportClassCouplingWithDeltas
            {
                Coupling = coupling,
                Deltas = comparison is { HasBaseline: true } ? BuildCouplingDeltas(comparison) : null,
            };
        }).ToList();

    private static CognitiveBaselineMethodSnapshot ToMethodSnapshot(CognitiveMetrics metrics) =>
        BaselineSnapshotFactory.FromMetrics(metrics);

    private static JsonReportMethodDeltas BuildMethodDeltas(MethodMetricsComparison comparison) =>
        new()
        {
            TotalScore = comparison.TotalScore.Delta,
            LinesOfCode = comparison.LinesOfCode.Delta,
            LinesOfCodeScore = comparison.LinesOfCodeScore.Delta,
            IfCount = comparison.IfCount.Delta,
            IfScore = comparison.IfScore.Delta,
            ElseCount = comparison.ElseCount.Delta,
            ElseScore = comparison.ElseScore.Delta,
            LoopCount = comparison.LoopCount.Delta,
            LoopScore = comparison.LoopScore.Delta,
            SwitchCount = comparison.SwitchCount.Delta,
            SwitchScore = comparison.SwitchScore.Delta,
            TryCatchCount = comparison.TryCatchCount.Delta,
            TryCatchScore = comparison.TryCatchScore.Delta,
            ReturnCount = comparison.ReturnCount.Delta,
            ReturnScore = comparison.ReturnScore.Delta,
            ArgumentCount = comparison.ArgumentCount.Delta,
            ArgumentScore = comparison.ArgumentScore.Delta,
            NestingLevels = comparison.NestingLevels.Delta,
            NestingScore = comparison.NestingScore.Delta,
            LocalVariableCount = comparison.LocalVariableCount.Delta,
            LocalVariableScore = comparison.LocalVariableScore.Delta,
            FieldAccessCount = comparison.FieldAccessCount.Delta,
            FieldAccessScore = comparison.FieldAccessScore.Delta,
            PropertyAccessCount = comparison.PropertyAccessCount.Delta,
            PropertyAccessScore = comparison.PropertyAccessScore.Delta,
            CyclomaticComplexity = comparison.CyclomaticComplexity.Delta,
            HalsteadVolume = comparison.HalsteadVolume.Delta,
            HalsteadDifficulty = comparison.HalsteadDifficulty.Delta,
            HalsteadEffort = comparison.HalsteadEffort.Delta,
            LineCoveragePercentage = comparison.LineCoveragePercentage.Delta,
            BranchCoveragePercentage = comparison.BranchCoveragePercentage.Delta,
            ChurnScore = comparison.ChurnScore.Delta,
        };

    private static JsonReportClassCouplingDeltas BuildCouplingDeltas(ClassCouplingComparison comparison) =>
        new()
        {
            IncomingCoupling = comparison.IncomingCoupling.Delta,
            OutgoingCoupling = comparison.OutgoingCoupling.Delta,
            Stability = comparison.Stability.Delta,
        };
}

internal sealed class JsonReportWithDeltas
{
    public int SchemaVersion { get; init; }

    public DateTimeOffset GeneratedAt { get; init; }

    public List<JsonReportMethodWithDeltas> Methods { get; init; } = [];

    public List<JsonReportClassCouplingWithDeltas> ClassCoupling { get; init; } = [];
}

internal sealed class JsonReportMethodWithDeltas
{
    public CognitiveBaselineMethodSnapshot Method { get; init; } = new();

    public JsonReportMethodDeltas? Deltas { get; init; }
}

internal sealed class JsonReportMethodDeltas
{
    public double? TotalScore { get; init; }

    public double? LinesOfCode { get; init; }

    public double? LinesOfCodeScore { get; init; }

    public double? IfCount { get; init; }

    public double? IfScore { get; init; }

    public double? ElseCount { get; init; }

    public double? ElseScore { get; init; }

    public double? LoopCount { get; init; }

    public double? LoopScore { get; init; }

    public double? SwitchCount { get; init; }

    public double? SwitchScore { get; init; }

    public double? TryCatchCount { get; init; }

    public double? TryCatchScore { get; init; }

    public double? ReturnCount { get; init; }

    public double? ReturnScore { get; init; }

    public double? ArgumentCount { get; init; }

    public double? ArgumentScore { get; init; }

    public double? NestingLevels { get; init; }

    public double? NestingScore { get; init; }

    public double? LocalVariableCount { get; init; }

    public double? LocalVariableScore { get; init; }

    public double? FieldAccessCount { get; init; }

    public double? FieldAccessScore { get; init; }

    public double? PropertyAccessCount { get; init; }

    public double? PropertyAccessScore { get; init; }

    public double? CyclomaticComplexity { get; init; }

    public double? HalsteadVolume { get; init; }

    public double? HalsteadDifficulty { get; init; }

    public double? HalsteadEffort { get; init; }

    public double? LineCoveragePercentage { get; init; }

    public double? BranchCoveragePercentage { get; init; }

    public double? ChurnScore { get; init; }
}

internal sealed class JsonReportClassCouplingWithDeltas
{
    public CognitiveBaselineClassCouplingSnapshot Coupling { get; init; } = new();

    public JsonReportClassCouplingDeltas? Deltas { get; init; }
}

internal sealed class JsonReportClassCouplingDeltas
{
    public double? IncomingCoupling { get; init; }

    public double? OutgoingCoupling { get; init; }

    public double? Stability { get; init; }
}
