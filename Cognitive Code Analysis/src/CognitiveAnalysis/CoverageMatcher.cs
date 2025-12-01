using CognitiveCodeAnalysis.CodeCoverage;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

/// <summary>
/// Matches code coverage data to cognitive metrics.
/// </summary>
public static class CoverageMatcher
{
    /// <summary>
    /// Matches coverage data to metrics collection.
    /// Primary match: method name + line number + file path
    /// Fallback match: class name + file path (for class-level coverage)
    /// </summary>
    /// <param name="metricsCollection">The cognitive metrics collection</param>
    /// <param name="coverageData">The coverage data from Cobertura report</param>
    /// <returns>Dictionary mapping metrics to their matched coverage data</returns>
    public static Dictionary<CognitiveMetrics, Coverage> MatchCoverageToMetrics(
        CognitiveMetricsCollection metricsCollection,
        IEnumerable<Coverage> coverageData
    )
    {
        var matches = new Dictionary<CognitiveMetrics, Coverage>();
        var coverageList = coverageData.ToList();

        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            Coverage? matchedCoverage = FindMatchingCoverage(metrics, coverageList);

            if (matchedCoverage != null)
            {
                matches[metrics] = matchedCoverage;
            }
        }

        return matches;
    }

    private static Coverage? FindMatchingCoverage(CognitiveMetrics metrics, List<Coverage> coverageList)
    {
        // Primary match: method name + line number + file path
        Coverage? methodMatch = coverageList.FirstOrDefault(c =>
            c.IsMethodLevel &&
            NormalizePath(c.FilePath) == NormalizePath(metrics.FilePath) &&
            c.MethodName == metrics.MethodName &&
            c.MethodLineNumber == metrics.methodLineNumber
        );

        if (methodMatch != null)
        {
            return methodMatch;
        }

        // Fallback match: class name + file path (for class-level coverage)
        Coverage? classMatch = coverageList.FirstOrDefault(c =>
            !c.IsMethodLevel &&
            NormalizePath(c.FilePath) == NormalizePath(metrics.FilePath) &&
            (c.FullyQualifiedClassName == metrics.ClassName ||
             c.FullyQualifiedClassName.EndsWith("." + metrics.ClassName) ||
             metrics.ClassName.EndsWith("." + c.FullyQualifiedClassName))
        );

        return classMatch;
    }

    /// <summary>
    /// Normalizes file paths for comparison by converting to absolute paths and standardizing separators.
    /// </summary>
    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        try
        {
            // Convert to absolute path if relative, then normalize separators
            string absolutePath = Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(path);

            // Normalize directory separators (handle both / and \)
            return absolutePath.Replace('\\', '/').TrimEnd('/');
        }
        catch
        {
            // If path is invalid, just normalize separators
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }
}

