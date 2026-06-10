/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

public sealed class CognitiveBaselineComparison
{
    private readonly Dictionary<string, MethodMetricsComparison> _methodsByKey;
    private readonly Dictionary<string, ClassCouplingComparison> _classCouplingByName;

    internal CognitiveBaselineComparison(
        Dictionary<string, MethodMetricsComparison> methodsByKey,
        Dictionary<string, ClassCouplingComparison> classCouplingByName
    )
    {
        _methodsByKey = methodsByKey;
        _classCouplingByName = classCouplingByName;
    }

    public bool HasBaseline => true;

    public bool TryGetMethodComparison(CognitiveMetrics metrics, out MethodMetricsComparison? comparison)
    {
        var key = BaselineMethodKey.FromMetrics(metrics);
        if (_methodsByKey.TryGetValue(key, out MethodMetricsComparison? value))
        {
            comparison = value;
            return true;
        }

        comparison = null;
        return false;
    }

    public bool TryGetClassCouplingComparison(string className, out ClassCouplingComparison? comparison)
    {
        if (_classCouplingByName.TryGetValue(className, out ClassCouplingComparison? value))
        {
            comparison = value;
            return true;
        }

        comparison = null;
        return false;
    }
}
