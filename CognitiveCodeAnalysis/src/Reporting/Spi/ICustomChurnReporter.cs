/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Reporting.Spi;

/// <summary>
/// A report for the <c>churn</c> command, supplied by a plugin assembly and selected with <c>churn -r &lt;key&gt;</c>.
/// The implementation needs a public parameterless constructor and is created once per run.
/// </summary>
public interface ICustomChurnReporter
{
    /// <summary>Renders the report. The tool writes the returned text to the output file.</summary>
    /// <returns>The complete report text. Returning <c>null</c> is an error.</returns>
    string Render(ChurnReportInput input);
}

/// <summary>What a custom churn reporter gets.</summary>
public sealed record ChurnReportInput
{
    /// <summary>One row per class, the highest churn first.</summary>
    public IReadOnlyList<ChurnRow> Classes { get; init; } = [];

    /// <summary>Whether a coverage report was given, so the coverage columns are meaningful even when empty.</summary>
    public bool CoverageProvided { get; init; }

    /// <summary>The day the changes are counted from.</summary>
    public DateOnly Since { get; init; }
}

/// <summary>The churn of one class: its complexity times how often its file changed.</summary>
public sealed record ChurnRow
{
    /// <summary>The name of the class.</summary>
    public string ClassName { get; init; } = "";

    /// <summary>The source file relative to the working directory with <c>/</c> separators, or absolute when it lies outside.</summary>
    public string FilePath { get; init; } = "";

    /// <summary>The sum of the cognitive scores of the methods of the class.</summary>
    public double Score { get; init; }

    /// <summary>How often the file changed since the start date.</summary>
    public int TimesChanged { get; init; }

    /// <summary>Score times changes.</summary>
    public double Churn { get; init; }

    /// <summary>The line coverage from 0.0 to 1.0, or null without coverage data for the class.</summary>
    public double? Coverage { get; init; }

    /// <summary>Churn weighted with the missing coverage, or null without coverage data.</summary>
    public double? RiskChurn { get; init; }

    /// <summary>One of CRITICAL, HIGH, MEDIUM, LOW, or null without coverage data.</summary>
    public string? RiskLevel { get; init; }
}
