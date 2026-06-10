/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

public sealed class MetricDelta
{
    public double? BaselineValue { get; init; }

    public double CurrentValue { get; init; }

    public double? Delta { get; init; }

    internal static MetricDelta FromRequired(double current, double baseline) =>
        new()
        {
            BaselineValue = baseline,
            CurrentValue = current,
            Delta = current - baseline,
        };

    internal static MetricDelta FromOptional(double? current, double? baseline)
    {
        if (!current.HasValue || !baseline.HasValue)
        {
            return new MetricDelta
            {
                BaselineValue = baseline,
                CurrentValue = current ?? 0,
                Delta = null,
            };
        }

        return FromRequired(current.Value, baseline.Value);
    }

    internal static MetricDelta FromCurrentOnly(double current) =>
        new()
        {
            CurrentValue = current,
        };
}
