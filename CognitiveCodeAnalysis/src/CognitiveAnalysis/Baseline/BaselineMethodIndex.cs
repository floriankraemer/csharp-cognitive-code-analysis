/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

/// <summary>
/// Finds the baseline entry of a current method. Baselines written with absolute paths, or before nested types of
/// structs, records and interfaces were named with their container, must still match, so the lookup falls back from
/// the exact key to ever looser, but always unambiguous, matches.
/// </summary>
internal sealed class BaselineMethodIndex
{
    private readonly Dictionary<string, CognitiveBaselineMethodSnapshot> _byKey = new(StringComparer.Ordinal);
    private readonly Dictionary<(string ClassName, string Signature), List<CognitiveBaselineMethodSnapshot>> _byClassAndSignature = [];
    private readonly Dictionary<string, List<CognitiveBaselineMethodSnapshot>> _bySignature = new(StringComparer.Ordinal);

    internal BaselineMethodIndex(IEnumerable<CognitiveBaselineMethodSnapshot> methods)
    {
        // The last entry of a duplicated key wins, as it always did.
        foreach (var method in methods)
        {
            _byKey[BaselineMethodKey.FromSnapshot(method)] = method;
        }

        foreach (var method in _byKey.Values)
        {
            Add(_byClassAndSignature, (method.ClassName, method.MethodSignature), method);
            Add(_bySignature, method.MethodSignature, method);
        }
    }

    private static void Add<TKey>(
        Dictionary<TKey, List<CognitiveBaselineMethodSnapshot>> index,
        TKey key,
        CognitiveBaselineMethodSnapshot method) where TKey : notnull
    {
        if (!index.TryGetValue(key, out var list))
        {
            index[key] = list = [];
        }

        list.Add(method);
    }

    internal CognitiveBaselineMethodSnapshot? Find(CognitiveMetrics metrics)
    {
        if (_byKey.TryGetValue(BaselineMethodKey.FromMetrics(metrics), out var exact)
            || _byKey.TryGetValue(BaselineMethodKey.AbsoluteFromMetrics(metrics), out exact))
        {
            return exact;
        }

        var sameClass = _byClassAndSignature.GetValueOrDefault((metrics.ClassName, metrics.methodSignature)) ?? [];
        return SingleOrNull(sameClass.Where(m => SameFile(m.FilePath, metrics.FilePath)))
            ?? SingleOrNull(sameClass)
            ?? SingleOrNull((_bySignature.GetValueOrDefault(metrics.methodSignature) ?? [])
                .Where(m => SameFile(m.FilePath, metrics.FilePath) && IsRenamedFrom(m.ClassName, metrics.ClassName)));
    }

    /// <summary>The coupling of a class under its name, or, for a renamed nested class, under its only older name.</summary>
    internal static CognitiveBaselineClassCouplingSnapshot? FindCoupling(
        string className,
        IReadOnlyList<CognitiveBaselineClassCouplingSnapshot> coupling)
    {
        var exact = coupling.LastOrDefault(c => c.ClassName == className);
        return exact ?? SingleOrNull(coupling.Where(c => IsRenamedFrom(c.ClassName, className)));
    }

    private static CognitiveBaselineMethodSnapshot? SingleOrNull(IEnumerable<CognitiveBaselineMethodSnapshot> candidates)
    {
        var list = candidates.Take(2).ToList();
        return list.Count == 1 ? list[0] : null;
    }

    private static CognitiveBaselineClassCouplingSnapshot? SingleOrNull(IEnumerable<CognitiveBaselineClassCouplingSnapshot> candidates)
    {
        var list = candidates.Take(2).ToList();
        return list.Count == 1 ? list[0] : null;
    }

    /// <summary>The same file, where one path may be relative to the working directory and the other absolute.</summary>
    private static bool SameFile(string baselinePath, string currentPath)
    {
        string baseline = CognitiveCiEncoding.NormalizeFilePath(baselinePath);
        string current = CognitiveCiEncoding.NormalizeFilePath(CognitiveCiLocation.DisplayPath(currentPath));
        return baseline == current
            || baseline.EndsWith("/" + current, StringComparison.Ordinal)
            || current.EndsWith("/" + baseline, StringComparison.Ordinal);
    }

    /// <summary>
    /// Whether the older name is the current one without some containing types, such as <c>N.Inner</c> for
    /// <c>N.Holder.Inner</c> after structs, records and interfaces became part of the name.
    /// </summary>
    internal static bool IsRenamedFrom(string baselineName, string currentName)
    {
        string[] older = baselineName.Split('.');
        string[] current = currentName.Split('.');
        if (older.Length >= current.Length || older[^1] != current[^1])
        {
            return false;
        }

        int next = 0;
        foreach (string segment in current)
        {
            if (next < older.Length && segment == older[next])
            {
                next++;
            }
        }

        return next == older.Length;
    }
}
