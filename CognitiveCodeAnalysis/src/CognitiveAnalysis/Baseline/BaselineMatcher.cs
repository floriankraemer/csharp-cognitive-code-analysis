/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

/// <summary>
/// Pairs the methods and classes of a run with those of a baseline. Baselines with absolute paths, or written before
/// nested types of structs, records and interfaces were named with their container, must still match, so unmatched
/// entries fall back to looser rules. A baseline entry is claimed at most once, the fallbacks only look at entries the
/// exact pass left over, and an ambiguous candidate on either side means no match.
/// </summary>
internal static class BaselineMatcher
{
    internal static Dictionary<CognitiveMetrics, CognitiveBaselineMethodSnapshot> MatchMethods(
        IEnumerable<CognitiveMetrics> current,
        IEnumerable<CognitiveBaselineMethodSnapshot> baseline)
    {
        var matches = new Dictionary<CognitiveMetrics, CognitiveBaselineMethodSnapshot>(ReferenceEqualityComparer.Instance);

        // The last entry of a duplicated key wins, as it always did.
        var byKey = new Dictionary<string, CognitiveBaselineMethodSnapshot>(StringComparer.Ordinal);
        foreach (var method in baseline)
        {
            byKey[Key(method.FilePath, method.ClassName, method.MethodSignature)] = method;
        }

        var unclaimed = byKey.Values.ToHashSet();
        var unmatched = new List<CognitiveMetrics>();
        foreach (var metrics in current)
        {
            if (byKey.TryGetValue(Key(metrics.FilePath, metrics.ClassName, metrics.methodSignature), out var exact)
                && unclaimed.Remove(exact))
            {
                matches[metrics] = exact;
            }
            else
            {
                unmatched.Add(metrics);
            }
        }

        // The file moved or was renamed: the same class and signature.
        Pair(unmatched, unclaimed, matches, (m, b) => m.ClassName == b.ClassName);

        // A nested class gained a containing type: the same file and signature, one more segment in the name.
        Pair(unmatched, unclaimed, matches, (m, b) =>
            SameFile(m.FilePath, b.FilePath) && IsRenamedFrom(b.ClassName, m.ClassName));

        return matches;
    }

    private static void Pair(
        List<CognitiveMetrics> unmatched,
        HashSet<CognitiveBaselineMethodSnapshot> unclaimed,
        Dictionary<CognitiveMetrics, CognitiveBaselineMethodSnapshot> matches,
        Func<CognitiveMetrics, CognitiveBaselineMethodSnapshot, bool> compatible)
    {
        var bySignature = unclaimed.ToLookup(b => b.MethodSignature, StringComparer.Ordinal);
        var candidates = new Dictionary<CognitiveMetrics, List<CognitiveBaselineMethodSnapshot>>(ReferenceEqualityComparer.Instance);
        var claimants = new Dictionary<CognitiveBaselineMethodSnapshot, int>();
        foreach (var metrics in unmatched)
        {
            var found = bySignature[metrics.methodSignature].Where(b => compatible(metrics, b)).ToList();
            candidates[metrics] = found;
            foreach (var entry in found)
            {
                claimants[entry] = claimants.GetValueOrDefault(entry) + 1;
            }
        }

        foreach (var metrics in unmatched.ToList())
        {
            if (candidates[metrics] is [var only] && claimants[only] == 1)
            {
                matches[metrics] = only;
                unclaimed.Remove(only);
                unmatched.Remove(metrics);
            }
        }
    }

    /// <summary>The baseline coupling of every current class that has one, by the class name of the current run.</summary>
    internal static Dictionary<string, CognitiveBaselineClassCouplingSnapshot> MatchCoupling(
        IEnumerable<CognitiveMetrics> current,
        IEnumerable<CognitiveBaselineClassCouplingSnapshot> coupling,
        IEnumerable<CognitiveBaselineMethodSnapshot> baselineMethods)
    {
        var byName = new Dictionary<string, CognitiveBaselineClassCouplingSnapshot>(StringComparer.Ordinal);
        foreach (var entry in coupling)
        {
            byName[entry.ClassName] = entry;
        }

        var currentFiles = FilesByClass(current.Select(m => (m.ClassName, m.FilePath)));
        var baselineFiles = FilesByClass(baselineMethods.Select(m => (m.ClassName, m.FilePath)));
        var matches = new Dictionary<string, CognitiveBaselineClassCouplingSnapshot>(StringComparer.Ordinal);

        // A rename is only considered for classes that have no counterpart under the same name on the other side.
        var renamedCandidates = byName.Values.Where(b => !currentFiles.ContainsKey(b.ClassName)).ToList();
        var candidates = new Dictionary<string, List<CognitiveBaselineClassCouplingSnapshot>>(StringComparer.Ordinal);
        var claimants = new Dictionary<CognitiveBaselineClassCouplingSnapshot, int>();
        foreach (var (className, files) in currentFiles)
        {
            if (byName.TryGetValue(className, out var exact))
            {
                matches[className] = exact;
                continue;
            }

            var found = renamedCandidates
                .Where(b => IsRenamedFrom(b.ClassName, className)
                    && SharesFile(files, baselineFiles.GetValueOrDefault(b.ClassName)))
                .ToList();
            candidates[className] = found;
            foreach (var entry in found)
            {
                claimants[entry] = claimants.GetValueOrDefault(entry) + 1;
            }
        }

        foreach (var (className, found) in candidates)
        {
            if (found is [var only] && claimants[only] == 1)
            {
                matches[className] = only;
            }
        }

        return matches;
    }

    private static Dictionary<string, HashSet<string>> FilesByClass(IEnumerable<(string ClassName, string FilePath)> methods)
    {
        var files = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (className, filePath) in methods)
        {
            if (!files.TryGetValue(className, out var set))
            {
                files[className] = set = new HashSet<string>(StringComparer.Ordinal);
            }

            set.Add(NormalizedPath(filePath));
        }

        return files;
    }

    /// <summary>Without file information on one side, as for a class that has no methods, only the name decides.</summary>
    private static bool SharesFile(HashSet<string> current, HashSet<string>? baseline) =>
        baseline == null || baseline.Count == 0 || current.Overlaps(baseline);

    private static string Key(string filePath, string className, string signature) =>
        NormalizedPath(filePath) + "|" + className + "|" + signature;

    /// <summary>The path relative to the working directory when it lies below it, with <c>/</c> separators.</summary>
    private static string NormalizedPath(string filePath) =>
        CognitiveCiLocation.DisplayPath(CognitiveCiEncoding.NormalizeFilePath(filePath));

    private static bool SameFile(string currentPath, string baselinePath) =>
        NormalizedPath(currentPath) == NormalizedPath(baselinePath);

    /// <summary>
    /// Whether the current name is the older one with exactly one containing type inserted, such as
    /// <c>N.Holder.Inner</c> for <c>N.Inner</c>, after structs, records and interfaces became part of the name.
    /// </summary>
    internal static bool IsRenamedFrom(string baselineName, string currentName)
    {
        string[] older = baselineName.Split('.');
        string[] current = currentName.Split('.');
        if (older.Length + 1 != current.Length)
        {
            return false;
        }

        for (int inserted = 0; inserted < current.Length - 1; inserted++)
        {
            if (current.Where((_, i) => i != inserted).SequenceEqual(older))
            {
                return true;
            }
        }

        return false;
    }
}
