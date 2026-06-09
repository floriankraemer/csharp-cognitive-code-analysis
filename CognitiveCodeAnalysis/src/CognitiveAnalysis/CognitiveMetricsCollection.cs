/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Collections.ObjectModel;

using CognitiveCodeAnalysis.CouplingAnalysis;

using Microsoft.Extensions.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class CognitiveMetricsCollection: Collection<CognitiveMetrics>
{
    private readonly Dictionary<string, ClassCouplingMetrics> _classCouplingByName =
        new(StringComparer.Ordinal);

    public void SetClassCouplingMetrics(IEnumerable<ClassCouplingMetrics> metrics)
    {
        _classCouplingByName.Clear();
        foreach (ClassCouplingMetrics metric in metrics)
        {
            _classCouplingByName[metric.ClassName] = metric;
        }
    }

    public bool TryGetClassCoupling(string className, out ClassCouplingMetrics? coupling)
    {
        if (_classCouplingByName.TryGetValue(className, out ClassCouplingMetrics? value))
        {
            coupling = value;
            return true;
        }

        coupling = null;
        return false;
    }

    public CognitiveMetricsCollection OnlyMetricsExceedingScoreThreshold(double scoreThreshold)
    {
        CognitiveMetricsCollection filtered = [];

        foreach (CognitiveMetrics metrics in this)
        {
            if (metrics.totalScore > scoreThreshold)
            {
                filtered.Add(metrics);
            }
        }

        return filtered;
    }

    public bool HasCoverageData()
    {
       return this.Any(m => m.HasCoverageData);
    }

    /// <summary>
    /// Counts the number of unique classes in the collection.
    /// Classes are considered unique based on their ClassName and FilePath combination.
    /// </summary>
    /// <returns>The total number of unique classes</returns>
    public int GetTotalClasses()
    {
        return this
            .GroupBy(metrics => new { metrics.ClassName, metrics.FilePath })
            .Count();
    }

    /// <summary>
    /// Gets the total number of methods in the collection.
    /// </summary>
    /// <returns>The total number of methods</returns>
    public int GetTotalMethods()
    {
        return Count;
    }

    /// <summary>
    /// Counts the number of methods whose total score exceeds the specified threshold.
    /// </summary>
    /// <param name="threshold">The score threshold to compare against</param>
    /// <returns>The number of methods exceeding the threshold</returns>
    public int GetMethodsExceedingThreshold(double threshold)
    {
        return this
            .Count(metrics => metrics.totalScore > threshold);
    }

    /// <summary>
    /// Counts the number of classes that have at least one method exceeding the specified threshold.
    /// Classes are identified by their ClassName and FilePath combination.
    /// </summary>
    /// <param name="threshold">The score threshold to compare against</param>
    /// <returns>The number of classes with at least one method exceeding the threshold</returns>
    public int GetClassesWithExceedingMethods(double threshold)
    {
        return this
            .GroupBy(metrics => new { metrics.ClassName, metrics.FilePath })
            .Count(g => g.Any(metrics => metrics.totalScore > threshold));
    }

    /// <summary>
    /// Calculates the percentage of methods that exceed the specified threshold.
    /// </summary>
    /// <param name="threshold">The score threshold to compare against</param>
    /// <returns>The percentage of methods exceeding the threshold.</returns>
    public double GetMethodsPercentage(double threshold)
    {
        int totalMethods = GetTotalMethods();
        if (totalMethods == 0)
        {
            return 0.0;
        }

        int methodsExceedingThreshold = GetMethodsExceedingThreshold(threshold);
        return (double)methodsExceedingThreshold / totalMethods * 100.0;
    }

    /// <summary>
    /// Calculates the percentage of classes that have at least one method exceeding the specified threshold.
    /// </summary>
    /// <param name="threshold">The score threshold to compare against</param>
    /// <returns>The percentage of classes with exceeding methods (0.0 to 100.0), or 0.0 if there are no classes</returns>
    public double GetClassesPercentage(double threshold)
    {
        int totalClasses = GetTotalClasses();
        if (totalClasses == 0)
        {
            return 0.0;
        }

        int classesWithExceedingMethods = GetClassesWithExceedingMethods(threshold);

        return (double)classesWithExceedingMethods / totalClasses * 100.0;
    }
}
