/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

public sealed class CognitiveBaselineSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public DateTimeOffset GeneratedAt { get; init; }

    public List<CognitiveBaselineMethodSnapshot> Methods { get; init; } = [];

    public List<CognitiveBaselineClassCouplingSnapshot> ClassCoupling { get; init; } = [];
}

public sealed class CognitiveBaselineMethodSnapshot
{
    public string FilePath { get; init; } = "";

    public string ClassName { get; init; } = "";

    public string MethodName { get; init; } = "";

    public string MethodSignature { get; init; } = "";

    public int MethodLineNumber { get; init; }

    public double TotalScore { get; init; }

    public int LinesOfCode { get; init; }

    public double LinesOfCodeScore { get; init; }

    public int IfCount { get; init; }

    public double IfScore { get; init; }

    public int ElseCount { get; init; }

    public double ElseScore { get; init; }

    public int LoopCount { get; init; }

    public double LoopScore { get; init; }

    public int SwitchCount { get; init; }

    public double SwitchScore { get; init; }

    public int TryCatchCount { get; init; }

    public double TryCatchScore { get; init; }

    public int ReturnCount { get; init; }

    public double ReturnScore { get; init; }

    public int ArgumentCount { get; init; }

    public double ArgumentScore { get; init; }

    public int NestingLevels { get; init; }

    public double NestingScore { get; init; }

    public int LocalVariableCount { get; init; }

    public double LocalVariableScore { get; init; }

    public int FieldAccessCount { get; init; }

    public double FieldAccessScore { get; init; }

    public int PropertyAccessCount { get; init; }

    public double PropertyAccessScore { get; init; }

    public double CyclomaticComplexity { get; init; }

    public double? HalsteadVolume { get; init; }

    public double? HalsteadDifficulty { get; init; }

    public double? HalsteadEffort { get; init; }

    public double? LineCoveragePercentage { get; init; }

    public double? BranchCoveragePercentage { get; init; }

    public double? ChurnScore { get; init; }
}

public sealed class CognitiveBaselineClassCouplingSnapshot
{
    public string ClassName { get; init; } = "";

    public int IncomingCoupling { get; init; }

    public int OutgoingCoupling { get; init; }

    public double Stability { get; init; }
}
