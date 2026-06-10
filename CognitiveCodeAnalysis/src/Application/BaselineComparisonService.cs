/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

namespace CognitiveCodeAnalysis.Application;

public sealed class BaselineComparisonService
{
    public CognitiveBaselineComparison? CompareIfRequested(
        string? baselineFile,
        CognitiveMetricsCollection metricsCollection
    ) {
        if (string.IsNullOrWhiteSpace(baselineFile))
        {
            return null;
        }

        var baseline = BaselineLoader.Load(baselineFile);
        return BaselineComparer.Compare(metricsCollection, baseline);
    }
}
