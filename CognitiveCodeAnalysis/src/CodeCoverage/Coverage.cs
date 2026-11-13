/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// Code coverage metrics DTO
/// </summary>
public class Coverage
{
    /// <summary>
    /// Fully Qualified Class Name (FQCN) of the class containing the method.
    /// </summary>
    public string FullyQualifiedClassName { get; set; } = string.Empty;

    /// <summary>
    /// File path where the class/method is located.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Name of the method. Empty string if this represents class-level coverage.
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// Line number where the method starts. 0 if this represents class-level coverage.
    /// </summary>
    public int MethodLineNumber { get; set; }

    /// <summary>
    /// Number of lines covered by tests.
    /// </summary>
    public int LinesCovered { get; set; }

    /// <summary>
    /// Total number of lines in the method or class.
    /// </summary>
    public int LinesTotal { get; set; }

    /// <summary>
    /// Line coverage as a fraction (0-1) for reports that carry only a rate and no line counts.
    /// Ignored when <see cref="LinesTotal"/> is positive.
    /// </summary>
    public double? LineRate { get; set; }

    /// <summary>
    /// Line coverage percentage (0-100). Computed from the counts when present, otherwise from <see cref="LineRate"/>.
    /// </summary>
    public double LineCoveragePercentage => LinesTotal > 0
        ? (LinesCovered / (double)LinesTotal) * 100
        : (LineRate ?? 0) * 100;

    /// <summary>
    /// Number of branches covered by tests.
    /// </summary>
    public int BranchesCovered { get; set; }

    /// <summary>
    /// Total number of branches in the method or class.
    /// </summary>
    public int BranchesTotal { get; set; }

    /// <summary>
    /// Branch coverage as a fraction (0-1) for reports that carry only a rate and no branch counts.
    /// Stays null when the report has no branch data, which includes methods without branches.
    /// Ignored when <see cref="BranchesTotal"/> is positive.
    /// </summary>
    public double? BranchRate { get; set; }

    /// <summary>
    /// Branch coverage percentage (0-100), or null when there is no branch data
    /// (for example a method without branches).
    /// </summary>
    public double? BranchCoveragePercentage => BranchesTotal > 0
        ? (BranchesCovered / (double)BranchesTotal) * 100
        : BranchRate * 100;

    /// <summary>
    /// Cyclomatic complexity of the method or class.
    /// </summary>
    public int Complexity { get; set; }

    /// <summary>
    /// Indicates whether this coverage entry represents a method (true) or class-level metrics (false).
    /// </summary>
    public bool IsMethodLevel => !string.IsNullOrEmpty(MethodName);
}
