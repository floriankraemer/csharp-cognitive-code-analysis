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
    /// Primary match: method name + file path, where the first coverage line of the method lies within the
    /// method's source lines (the declaration line up to its last line). Coverage writers report the first
    /// executable line, not the line of the declaration, so exact line equality would miss real reports.
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
        // key: "normalizedPath|methodName"; overloads share a key and are told apart by their first line
        private readonly Dictionary<string, List<Coverage>> _methodLevel;

        // method keys whose coverage includes compiler-generated types; they never use class-level coverage
        private readonly HashSet<string> _generatedMethods;

        // key: "normalizedPath|fullyQualifiedClassName"  (class-level entries only)
        private readonly Dictionary<string, Coverage> _classLevel;

        // key: normalizedPath  (file-aggregate entries: empty FQCN, no method name)
        private readonly Dictionary<string, Coverage> _fileLevel;

        private CoverageIndex(
            Dictionary<string, List<Coverage>> methodLevel,
            HashSet<string> generatedMethods,
            Dictionary<string, Coverage> classLevel,
            Dictionary<string, Coverage> fileLevel
        ) {
            _methodLevel = methodLevel;
            _generatedMethods = generatedMethods;
            _classLevel = classLevel;
            _fileLevel = fileLevel;
        }

        internal static CoverageIndex Build(List<Coverage> coverageList)
        {
            var methodLevel = new Dictionary<string, List<Coverage>>(StringComparer.OrdinalIgnoreCase);
            var generatedMethods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var classLevel = new Dictionary<string, Coverage>(StringComparer.OrdinalIgnoreCase);
            var fileLevel = new Dictionary<string, Coverage>(StringComparer.OrdinalIgnoreCase);

            foreach (Coverage c in coverageList)
            {
                string normalizedPath = NormalizePath(c.FilePath);

                if (c.IsMethodLevel)
                {
                    // Primary: method name + file, the line is checked on lookup
                    string key = MethodKey(normalizedPath, c.MethodName);
                    if (!methodLevel.TryGetValue(key, out List<Coverage>? candidates))
                    {
                        candidates = [];
                        methodLevel[key] = candidates;
                    }

                    candidates.Add(c);
                    if (c.FromGeneratedTypes)
                    {
                        generatedMethods.Add(key);
                    }
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

            return new CoverageIndex(methodLevel, generatedMethods, classLevel, fileLevel);
        }

        internal Coverage? Find(CognitiveMetrics metrics)
        {
            string normalizedPath = NormalizePath(metrics.FilePath);

            // 1. Method-level match: same name and first coverage line inside the method's lines
            Coverage? methodMatch = FindMethodLevelMatch(normalizedPath, metrics);
            if (methodMatch != null)
            {
                return methodMatch;
            }

            // The class-level figures say nothing about an async, iterator or closure body, so report no data instead.
            if (_generatedMethods.Contains(MethodKey(normalizedPath, CoverageMethodName(metrics))))
            {
                return null;
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

        private Coverage? FindMethodLevelMatch(string normalizedPath, CognitiveMetrics metrics)
        {
            if (!_methodLevel.TryGetValue(MethodKey(normalizedPath, CoverageMethodName(metrics)), out List<Coverage>? candidates))
            {
                return null;
            }

            int firstLine = metrics.methodLineNumber;
            int endLine = firstLine + Math.Max(metrics.linesOfCode, 1);

            return candidates.FirstOrDefault(c => c.MethodLineNumber >= firstLine && c.MethodLineNumber < endLine);
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

        /// <summary>
        /// Coverage writers name constructors after their runtime names, <c>.ctor</c> and <c>.cctor</c> for the static one.
        /// C# forbids a member named like its enclosing type, so a method named like its class is a constructor.
        /// </summary>
        private static string CoverageMethodName(CognitiveMetrics metrics)
        {
            string typeName = metrics.ClassName[(metrics.ClassName.LastIndexOf('.') + 1)..];
            if (metrics.MethodName != typeName)
            {
                return metrics.MethodName;
            }

            return metrics.methodSignature.Split(' ').Contains("static") ? ".cctor" : ".ctor";
        }

        private static string MethodKey(string normalizedPath, string methodName)
            => $"{normalizedPath}|{methodName}";

        private static string ClassKey(string normalizedPath, string fqcn)
            => $"{normalizedPath}|{fqcn}";
    }
}
