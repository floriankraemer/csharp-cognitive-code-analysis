/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Reporting.Spi;

/// <summary>
/// A report for the <c>analyse</c> command, supplied by a plugin assembly and selected with <c>-r &lt;key&gt;</c>.
/// The implementation needs a public parameterless constructor and is created once per run.
/// </summary>
public interface ICustomCognitiveReporter
{
    /// <summary>Renders the report. The tool writes the returned text to the output file.</summary>
    /// <returns>The complete report text. Returning <c>null</c> is an error.</returns>
    string Render(CognitiveReportInput input);
}

/// <summary>What a custom analyse reporter gets: the analysed methods and the settings of the run.</summary>
public sealed record CognitiveReportInput
{
    /// <summary>All analysed methods, in the order of <c>--sort-by</c>, otherwise by total score, highest first. Not filtered by the score threshold.</summary>
    public IReadOnlyList<CognitiveMethodRow> Methods { get; init; } = [];

    /// <summary>The effective settings of the run.</summary>
    public CognitiveReportSettings Settings { get; init; } = new();

    /// <summary>Whether a baseline was given. Only then can <see cref="CognitiveMethodRow.Deltas"/> be set.</summary>
    public bool HasBaseline { get; init; }
}

/// <summary>The effective settings of an analyse run.</summary>
public sealed record CognitiveReportSettings
{
    /// <summary>Methods with a total score above this value are the ones the standard reports flag.</summary>
    public double ScoreThreshold { get; init; }

    /// <summary>Whether the standard reports list only the methods above <see cref="ScoreThreshold"/>.</summary>
    public bool ShowOnlyMethodsExceedingThreshold { get; init; }

    /// <summary>Whether the standard reports group the methods by class.</summary>
    public bool GroupByClass { get; init; }

    /// <summary>Whether the Halstead measures were asked for.</summary>
    public bool ShowHalsteadComplexity { get; init; }

    /// <summary>Whether the cyclomatic complexity was asked for.</summary>
    public bool ShowCyclomaticComplexity { get; init; }

    /// <summary>Whether the class coupling was asked for.</summary>
    public bool ShowCouplingMetrics { get; init; }

    /// <summary>Whether the standard reports show the detail columns.</summary>
    public bool ShowDetailedCognitiveMetrics { get; init; }
}

/// <summary>The count of one kind of construct in a method and the score it adds.</summary>
/// <param name="Count">How often the construct occurs.</param>
/// <param name="Score">The score the occurrences add to the method.</param>
public sealed record CountedMetric(double Count, double Score);

/// <summary>One analysed method.</summary>
public sealed record CognitiveMethodRow
{
    /// <summary>The source file relative to the working directory with <c>/</c> separators, or absolute when it lies outside.</summary>
    public string FilePath { get; init; } = "";

    /// <summary>The name of the class, record, struct or interface that declares the method.</summary>
    public string ClassName { get; init; } = "";

    /// <summary>The name of the method.</summary>
    public string MethodName { get; init; } = "";

    /// <summary>The signature of the method.</summary>
    public string MethodSignature { get; init; } = "";

    /// <summary>The line the method starts on.</summary>
    public int Line { get; init; }

    /// <summary>The total cognitive score of the method.</summary>
    public double TotalScore { get; init; }

    /// <summary>Lines of code.</summary>
    public CountedMetric LinesOfCode { get; init; } = new(0, 0);

    /// <summary>Parameters.</summary>
    public CountedMetric Arguments { get; init; } = new(0, 0);

    /// <summary>Return statements.</summary>
    public CountedMetric Returns { get; init; } = new(0, 0);

    /// <summary>Local variables.</summary>
    public CountedMetric LocalVariables { get; init; } = new(0, 0);

    /// <summary>Property accesses.</summary>
    public CountedMetric PropertyAccesses { get; init; } = new(0, 0);

    /// <summary>Field accesses.</summary>
    public CountedMetric FieldAccesses { get; init; } = new(0, 0);

    /// <summary>If statements.</summary>
    public CountedMetric Ifs { get; init; } = new(0, 0);

    /// <summary>Else branches.</summary>
    public CountedMetric Elses { get; init; } = new(0, 0);

    /// <summary>Nesting levels.</summary>
    public CountedMetric Nesting { get; init; } = new(0, 0);

    /// <summary>Loops.</summary>
    public CountedMetric Loops { get; init; } = new(0, 0);

    /// <summary>Switch statements and expressions.</summary>
    public CountedMetric Switches { get; init; } = new(0, 0);

    /// <summary>Try/catch blocks.</summary>
    public CountedMetric TryCatches { get; init; } = new(0, 0);

    /// <summary>The cyclomatic complexity.</summary>
    public double CyclomaticComplexity { get; init; }

    /// <summary>The Halstead volume, or null when it was not measured.</summary>
    public double? HalsteadVolume { get; init; }

    /// <summary>The Halstead difficulty, or null when it was not measured.</summary>
    public double? HalsteadDifficulty { get; init; }

    /// <summary>The Halstead effort, or null when it was not measured.</summary>
    public double? HalsteadEffort { get; init; }

    /// <summary>The line coverage in percent, or null without coverage data.</summary>
    public double? LineCoverage { get; init; }

    /// <summary>The branch coverage in percent, or null without coverage data.</summary>
    public double? BranchCoverage { get; init; }

    /// <summary>The change against the baseline, or null when there is no baseline or the method is not in it.</summary>
    public CognitiveMethodDeltas? Deltas { get; init; }
}

/// <summary>The change of a method against the baseline: current minus baseline. A value is null when one side has no data.</summary>
public sealed record CognitiveMethodDeltas
{
    /// <summary>Change of the total score.</summary>
    public double? TotalScore { get; init; }

    /// <summary>Change of the cyclomatic complexity.</summary>
    public double? CyclomaticComplexity { get; init; }

    /// <summary>Change of the line coverage.</summary>
    public double? LineCoverage { get; init; }

    /// <summary>Change of the branch coverage.</summary>
    public double? BranchCoverage { get; init; }
}
