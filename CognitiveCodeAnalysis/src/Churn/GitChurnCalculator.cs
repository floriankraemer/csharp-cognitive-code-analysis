/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Churn;

/// <summary>
/// Aggregates method scores per class and multiplies them with the number of changes of the class's file.
/// Not to be confused with <see cref="CognitiveAnalysis.ChurnCalculator"/>, which weights a method's score with its
/// missing coverage (the "Churn" column of the analyse reports, called risk churn here).
/// </summary>
public static class GitChurnCalculator
{
    private const int Decimals = 3;

    /// <summary>
    /// One entry per class, sorted by churn descending, then by class name. The score of a class is the sum of its
    /// method scores. Changes are counted once per file. A class name that occurs in several files is merged into one
    /// entry that keeps the first file and the highest change count.
    /// </summary>
    public static List<ChurnMetrics> Calculate(
        IEnumerable<CognitiveMetrics> methods,
        IChangeCounter counter,
        ClassCoverageIndex? coverage = null)
    {
        var changesPerFile = new Dictionary<string, int>(StringComparer.Ordinal);
        var classes = new Dictionary<string, Totals>(StringComparer.Ordinal);

        foreach (CognitiveMetrics method in methods)
        {
            if (!changesPerFile.TryGetValue(method.FilePath, out int timesChanged))
            {
                timesChanged = counter.Count(method.FilePath);
                changesPerFile[method.FilePath] = timesChanged;
            }

            if (!classes.TryGetValue(method.ClassName, out Totals? totals))
            {
                totals = new Totals(method.FilePath);
                classes[method.ClassName] = totals;
            }

            totals.Score += method.totalScore;
            totals.TimesChanged = Math.Max(totals.TimesChanged, timesChanged);
        }

        return classes
            .Select(entry => ChurnOf(entry.Key, entry.Value, coverage))
            .OrderByDescending(metric => metric.Churn)
            .ThenBy(metric => metric.ClassName, StringComparer.Ordinal)
            .ToList();
    }

    private static ChurnMetrics ChurnOf(string className, Totals totals, ClassCoverageIndex? coverage)
    {
        double score = Round(totals.Score);
        double churn = Churn(totals.TimesChanged, score);
        double? classCoverage = coverage?.Rate(className);

        return new ChurnMetrics(
            className,
            totals.File,
            score,
            totals.TimesChanged,
            churn,
            classCoverage,
            classCoverage is { } rate ? RiskChurn(totals.TimesChanged, score, rate) : null,
            classCoverage is { } level ? RiskLevel(churn, level) : null);
    }

    public static double Churn(int timesChanged, double score) => Round(timesChanged * score);

    /// <summary>times changed × score × (1 − coverage), with coverage between 0.0 and 1.0.</summary>
    public static double RiskChurn(int timesChanged, double score, double coverage) =>
        Round(timesChanged * score * (1 - coverage));

    public static ChurnRiskLevel RiskLevel(double churn, double coverage)
    {
        if (churn > 30 && coverage < 0.5)
        {
            return ChurnRiskLevel.Critical;
        }

        if (churn > 20 && coverage < 0.7)
        {
            return ChurnRiskLevel.High;
        }

        return churn > 10 && coverage < 0.8 ? ChurnRiskLevel.Medium : ChurnRiskLevel.Low;
    }

    private static double Round(double value) =>
        (double)Math.Round((decimal)value, Decimals, MidpointRounding.AwayFromZero);

    private sealed class Totals(string file)
    {
        public string File { get; } = file;
        public double Score { get; set; }
        public int TimesChanged { get; set; }
    }
}
