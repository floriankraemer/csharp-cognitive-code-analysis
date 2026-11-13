/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;

namespace CognitiveCodeAnalysis.Churn;

/// <summary>Line coverage per class, from the class-level entries of a coverage report.</summary>
public sealed class ClassCoverageIndex
{
    private readonly Dictionary<string, double> _byName = new(StringComparer.Ordinal);

    public ClassCoverageIndex(IEnumerable<Coverage> coverage)
    {
        foreach (Coverage entry in coverage)
        {
            if (entry.IsMethodLevel || entry.FullyQualifiedClassName.Length == 0 || entry.LinesTotal <= 0)
            {
                continue;
            }

            _byName.TryAdd(entry.FullyQualifiedClassName, entry.LinesCovered / (double)entry.LinesTotal);
        }
    }

    /// <summary>The line coverage (0.0 to 1.0) of the class, null when the report does not know it or it has no lines.</summary>
    public double? Rate(string className)
    {
        if (_byName.TryGetValue(className, out double exact))
        {
            return exact;
        }

        // A report may name "Engine" where the analysis says "MyNs.Engine", or the other way round.
        foreach ((string name, double rate) in _byName)
        {
            if (name.EndsWith("." + className, StringComparison.Ordinal)
                || className.EndsWith("." + name, StringComparison.Ordinal))
            {
                return rate;
            }
        }

        return null;
    }

    public bool IsEmpty => _byName.Count == 0;
}
