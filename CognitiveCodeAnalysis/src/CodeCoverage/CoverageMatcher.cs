/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// <![CDATA[Matches code coverage data to cognitive metrics.]]>
/// </summary>
public static class CoverageMatcher
{
    /// <summary>
    /// <![CDATA[
    /// Matches coverage data to metrics collection.
    /// Primary match: method name + line number + file path (O(1) lookup)
    /// Fallback match: class name + file path (O(1) lookup)
    /// Tertiary match: file path only — file-level aggregate (O(1) lookup)
    ///
    /// All three lookup levels are pre-indexed so the overall algorithm is O(N+M)
    /// rather than the original O(N×M) with repeated linear scans.
    /// ]]>
    /// </summary>
    public static Dictionary<CognitiveMetrics, Coverage> MatchCoverageToMetrics(
        CognitiveMetricsCollection metricsCollection,
        IEnumerable<Coverage> coverageData
    ) => MatchCoverageToMetrics(metricsCollection, coverageData, progress: null);

    public static Dictionary<CognitiveMetrics, Coverage> MatchCoverageToMetrics(
        CognitiveMetricsCollection metricsCollection,
        IEnumerable<Coverage> coverageData,
        IProgress<AnalysisProgress>? progress
    ) {
        var coverageList = coverageData.ToList();
        int totalMethods = metricsCollection.Count;

        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.ApplyingCoverage,
            TotalFiles: totalMethods,
            ProcessedFiles: 0
        ));

        CoverageIndex index = CoverageIndex.Build(coverageList);

        var matches = new Dictionary<CognitiveMetrics, Coverage>(totalMethods);
        const int progressBatchSize = 100;
        int processedMethods = 0;

        foreach (CognitiveMetrics metrics in metricsCollection)
        {
            Coverage? matched = index.Find(metrics);
            if (matched != null)
            {
                matches[metrics] = matched;
            }

            processedMethods++;
            if (processedMethods % progressBatchSize == 0 || processedMethods == totalMethods)
            {
                progress?.Report(new AnalysisProgress(
                    AnalysisProgressPhase.ApplyingCoverage,
                    TotalFiles: totalMethods,
                    ProcessedFiles: processedMethods
                ));
            }
        }

        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.CoverageApplied,
            TotalFiles: totalMethods,
            ProcessedFiles: totalMethods
        ));

        return matches;
    }

    internal static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        try
        {
            string absolutePath = Path.IsPathRooted(path) ? path : Path.GetFullPath(path);
            return absolutePath.Replace('\\', '/').TrimEnd('/');
        }
        catch
        {
            return path.Replace('\\', '/').TrimEnd('/');
        }
    }

    /// <summary>
    /// Pre-built three-level lookup so each metrics lookup is O(1) instead of O(M).
    /// </summary>
    private sealed class CoverageIndex
    {
        // key: "normalizedPath|methodName|lineNumber"
        private readonly Dictionary<string, Coverage> _methodLevel;

        // key: "normalizedPath|fullyQualifiedClassName"  (class-level entries only)
        private readonly Dictionary<string, Coverage> _classLevel;

        // key: normalizedPath  (file-aggregate entries: empty FQCN, no method name)
        private readonly Dictionary<string, Coverage> _fileLevel;

        private CoverageIndex(
            Dictionary<string, Coverage> methodLevel,
            Dictionary<string, Coverage> classLevel,
            Dictionary<string, Coverage> fileLevel
        ) {
            _methodLevel = methodLevel;
            _classLevel = classLevel;
            _fileLevel = fileLevel;
        }

        internal static CoverageIndex Build(List<Coverage> coverageList)
        {
            var methodLevel = new Dictionary<string, Coverage>(StringComparer.OrdinalIgnoreCase);
            var classLevel = new Dictionary<string, Coverage>(StringComparer.OrdinalIgnoreCase);
            var fileLevel = new Dictionary<string, Coverage>(StringComparer.OrdinalIgnoreCase);

            foreach (Coverage c in coverageList)
            {
                string normalizedPath = NormalizePath(c.FilePath);

                if (c.IsMethodLevel)
                {
                    // Primary: method name + line + file
                    string key = MethodKey(normalizedPath, c.MethodName, c.MethodLineNumber);
                    methodLevel.TryAdd(key, c);
                }
                else if (string.IsNullOrEmpty(c.FullyQualifiedClassName))
                {
                    // Tertiary: file-level aggregate
                    fileLevel.TryAdd(normalizedPath, c);
                }
                else
                {
                    // Secondary: class-level (add all FQCN variations so either direction matches)
                    string key = ClassKey(normalizedPath, c.FullyQualifiedClassName);
                    classLevel.TryAdd(key, c);
                }
            }

            return new CoverageIndex(methodLevel, classLevel, fileLevel);
        }

        internal Coverage? Find(CognitiveMetrics metrics)
        {
            string normalizedPath = NormalizePath(metrics.FilePath);

            // 1. Method-level exact match
            string methodKey = MethodKey(normalizedPath, metrics.MethodName, metrics.methodLineNumber);
            if (_methodLevel.TryGetValue(methodKey, out Coverage? methodMatch))
            {
                return methodMatch;
            }

            // 2. Class-level match — try both the full FQCN stored in the index and
            //    partial suffix-based lookups to replicate the original fallback logic.
            Coverage? classMatch = FindClassLevelMatch(normalizedPath, metrics.ClassName);
            if (classMatch != null)
            {
                return classMatch;
            }

            // 3. File-level aggregate
            return _fileLevel.TryGetValue(normalizedPath, out Coverage? fileMatch) ? fileMatch : null;
        }

        private Coverage? FindClassLevelMatch(string normalizedPath, string metricsClassName)
        {
            // Direct key for coverage FQCN == metrics ClassName
            string directKey = ClassKey(normalizedPath, metricsClassName);
            if (_classLevel.TryGetValue(directKey, out Coverage? direct))
            {
                return direct;
            }

            // Coverage FQCN ends with ".metricsClassName" (e.g. "MyNs.Engine" vs "Engine")
            // or metrics class name ends with ".coverageFQCN" — scan only the entries for
            // this path to keep worst case O(classes_per_file) instead of O(M).
            string prefix = normalizedPath + "|";
            foreach (KeyValuePair<string, Coverage> kv in _classLevel)
            {
                if (!kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string fqcn = kv.Value.FullyQualifiedClassName;
                if (fqcn.EndsWith("." + metricsClassName, StringComparison.Ordinal)
                    || metricsClassName.EndsWith("." + fqcn, StringComparison.Ordinal))
                {
                    return kv.Value;
                }
            }

            return null;
        }

        private static string MethodKey(string normalizedPath, string methodName, int lineNumber)
            => $"{normalizedPath}|{methodName}|{lineNumber}";

        private static string ClassKey(string normalizedPath, string fqcn)
            => $"{normalizedPath}|{fqcn}";
    }
}
