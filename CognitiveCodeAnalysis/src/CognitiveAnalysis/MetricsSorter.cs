/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

/// <summary>
/// Orders methods by one field for <c>--sort-by</c>. Numbers compare numerically, text ignoring the letter case.
/// Methods with an equal value stay in ascending order of class, method, file and line, in either direction.
/// </summary>
public sealed class MetricsSorter
{
    private const string Ascending = "asc";
    private const string Descending = "desc";

    private static readonly List<string> Listed = [];

    private static readonly Dictionary<string, string> ScoreAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["argumentScore"] = "argumentCountScore",
        ["returnScore"] = "returnCountScore",
        ["localVariableScore"] = "localVariableCountScore",
        ["propertyAccessScore"] = "propertyAccessCountScore",
        ["fieldAccessScore"] = "fieldAccessCountScore",
        ["ifScore"] = "ifCountScore",
        ["elseScore"] = "elseCountScore",
        ["nestingScore"] = "nestingLevelsScore",
        ["loopScore"] = "loopCountScore",
        ["switchScore"] = "switchCountScore",
        ["tryCatchScore"] = "tryCatchCountScore",
    };

    private static readonly Dictionary<string, Comparison<CognitiveMetrics>> Fields = BuildFields();

    private readonly Comparison<CognitiveMetrics>? _comparison;

    private MetricsSorter(Comparison<CognitiveMetrics>? comparison) => _comparison = comparison;

    /// <summary>The names <c>--sort-by</c> accepts, in the order to list them.</summary>
    public static IReadOnlyList<string> FieldNames => Listed;

    /// <param name="field">One of <see cref="FieldNames"/> in any letter case, or null to keep the given order.</param>
    /// <param name="order"><c>asc</c> or <c>desc</c> in any letter case. It is checked even when there is no field.</param>
    /// <exception cref="ArgumentException">The field or the order is unknown.</exception>
    public static MetricsSorter Create(string? field, string? order)
    {
        string normalizedOrder = (order ?? Ascending).Trim().ToLowerInvariant();
        if (normalizedOrder is not (Ascending or Descending))
        {
            throw new ArgumentException($"Invalid sort order \"{order}\". Valid values: {Ascending}, {Descending}.");
        }

        if (field is null)
        {
            return new MetricsSorter(null);
        }

        if (!Fields.TryGetValue(field.Trim(), out Comparison<CognitiveMetrics>? primary))
        {
            throw new ArgumentException(
                $"Invalid sort field \"{field}\". Valid fields: {string.Join(", ", FieldNames)}.");
        }

        bool descending = normalizedOrder == Descending;
        return new MetricsSorter((a, b) =>
        {
            int result = primary(a, b);
            if (result != 0)
            {
                return descending ? -result : result;
            }

            return TieBreak(a, b);
        });
    }

    /// <summary>Returns the methods in the requested order, or in the given order when there is no field.</summary>
    public IReadOnlyList<CognitiveMetrics> Sort(IEnumerable<CognitiveMetrics> metrics)
    {
        return _comparison is null
            ? metrics.ToList()
            : metrics.OrderBy(m => m, Comparer<CognitiveMetrics>.Create(_comparison)).ToList();
    }

    private static int TieBreak(CognitiveMetrics a, CognitiveMetrics b)
    {
        int result = Text(a.ClassName, b.ClassName);
        if (result == 0)
        {
            result = Text(a.MethodName, b.MethodName);
        }

        if (result == 0)
        {
            result = Text(a.FilePath, b.FilePath);
        }

        return result != 0 ? result : a.methodLineNumber.CompareTo(b.methodLineNumber);
    }

    private static int Text(string? a, string? b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, Comparison<CognitiveMetrics>> BuildFields()
    {
        var fields = new Dictionary<string, Comparison<CognitiveMetrics>>(StringComparer.OrdinalIgnoreCase);

        void Add(string name, Comparison<CognitiveMetrics> comparison)
        {
            fields[name] = comparison;
            Listed.Add(name);
        }

        void Number(string name, Func<CognitiveMetrics, double> value) =>
            Add(name, (a, b) => value(a).CompareTo(value(b)));
        void Words(string name, Func<CognitiveMetrics, string?> value) =>
            Add(name, (a, b) => Text(value(a), value(b)));

        Number("score", m => m.totalScore);
        Words("class", m => m.ClassName);
        Words("method", m => m.MethodName);
        Words("file", m => m.FilePath);
        Number("line", m => m.methodLineNumber);
        Number("halstead", m => m.Halstead?.Volume ?? 0);
        Number("cyclomatic", m => m.cyclomaticComplexity);
        Number("understandability", m => m.Understandability.Total);

        Number("linesOfCode", m => m.linesOfCode);
        Number("argumentCount", m => m.argumentCount);
        Number("returnCount", m => m.returnCount);
        Number("localVariableCount", m => m.localVariableCount);
        Number("propertyAccessCount", m => m.propertyAccessCount);
        Number("fieldAccessCount", m => m.fieldAccessCount);
        Number("ifCount", m => m.ifCount);
        Number("elseCount", m => m.elseCount);
        Number("nestingLevels", m => m.nestingLevels);
        Number("loopCount", m => m.loopCount);
        Number("switchCount", m => m.switchCount);
        Number("tryCatchCount", m => m.tryCatchCount);

        Number("linesOfCodeScore", m => m.linesOfCodeScore);
        Number("argumentCountScore", m => m.argumentScore);
        Number("returnCountScore", m => m.returnScore);
        Number("localVariableCountScore", m => m.localVariableScore);
        Number("propertyAccessCountScore", m => m.propertyAccessScore);
        Number("fieldAccessCountScore", m => m.fieldAccessScore);
        Number("ifCountScore", m => m.ifScore);
        Number("elseCountScore", m => m.elseScore);
        Number("nestingLevelsScore", m => m.nestingScore);
        Number("loopCountScore", m => m.loopScore);
        Number("switchCountScore", m => m.switchScore);
        Number("tryCatchCountScore", m => m.tryCatchScore);

        // The names of the score columns in the CSV and JSON reports are accepted too, but not listed.
        foreach ((string alias, string canonical) in ScoreAliases)
        {
            fields[alias] = fields[canonical];
        }

        return fields;
    }
}
