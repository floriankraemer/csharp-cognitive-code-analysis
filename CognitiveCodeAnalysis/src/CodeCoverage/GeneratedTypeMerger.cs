/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// <![CDATA[
/// The compiler turns async methods and iterators into nested state machine classes (Outer/<Method>d__N, method MoveNext)
/// and lambdas, closures and local functions into nested holder classes (Outer/<>c__DisplayClassN_M, method <Method>b__N).
/// Coverlet reports them as classes of their own. Their lines belong to the method they were written in, so this class
/// maps them to the outer class and method name and merges their line and branch data into that method.
/// ]]>
/// </summary>
internal sealed partial class GeneratedTypeMerger
{
    private sealed record Part(string Outer, string FilePath, string MethodName, XElement? StateMachineClass, XElement Element);

    private sealed record Target(Coverage Coverage, XElement? Original, List<XElement> Parts);

    private readonly List<Part> _parts = [];
    private readonly List<(string Outer, Coverage Coverage, XElement Element)> _normals = [];

    [GeneratedRegex(@"^<+(?<name>[^<>|]+)>[^/]*d__\d+$")]
    private static partial Regex StateMachineType();

    [GeneratedRegex(@"^<+(?<name>[^<>|]+)>")]
    private static partial Regex GeneratedMethodName();

    [GeneratedRegex(@"\((?<covered>\d+)/(?<total>\d+)\)")]
    private static partial Regex ConditionCoverage();

    /// <summary>
    /// Splits a Cobertura class name at its first compiler-generated segment.
    /// Returns false for ordinary classes, whose nesting separator '/' is only replaced by '.' by the caller.
    /// </summary>
    public static bool IsGenerated(string className, out string outer, out string? stateMachineMethod)
    {
        string[] segments = className.Split('/');
        int index = Array.FindIndex(segments, s => s.StartsWith('<'));
        outer = index < 0 ? className.Replace('/', '.') : string.Join('.', segments.Take(index));
        stateMachineMethod = null;
        if (index < 0)
        {
            return false;
        }

        Match match = StateMachineType().Match(segments[index]);
        if (match.Success)
        {
            stateMachineMethod = match.Groups["name"].Value;
        }

        return true;
    }

    public void AddGeneratedClass(string outer, string? stateMachineMethod, XElement classElement, string filePath)
    {
        foreach (XElement method in classElement.Elements("methods").Elements("method"))
        {
            string? name = stateMachineMethod;
            if (name == null && GeneratedMethodName().Match(method.Attribute("name")?.Value ?? string.Empty) is { Success: true } holder)
            {
                name = holder.Groups["name"].Value;
            }

            if (name != null)
            {
                _parts.Add(new Part(outer, filePath, name, stateMachineMethod != null ? classElement : null, method));
            }
        }
    }

    public void AddMethod(string outer, Coverage coverage, XElement element) => _normals.Add((outer, coverage, element));

    /// <summary>Adds the merged generated parts to <paramref name="coverageList"/> and updates the ordinary methods they belong to.</summary>
    public void MergeInto(List<Coverage> coverageList)
    {
        var targets = new List<Target>();
        var standalone = new Dictionary<(string, string, string, XElement?), Target>();

        foreach (Part part in _parts)
        {
            int firstLine = FirstLine(part.Element);
            var candidates = _normals
                .Where(n => n.Outer == part.Outer && n.Coverage.FilePath == part.FilePath && n.Coverage.MethodName == part.MethodName)
                .OrderBy(n => n.Coverage.MethodLineNumber)
                .ToList();

            if (candidates.Count > 0)
            {
                // Overloads are told apart by position: the part belongs to the nearest method starting at or before it.
                var owner = candidates.LastOrDefault(n => n.Coverage.MethodLineNumber <= firstLine);
                owner = owner == default ? candidates[0] : owner;
                Target target = targets.FirstOrDefault(t => t.Coverage == owner.Coverage)
                    ?? Add(targets, new Target(owner.Coverage, owner.Element, []));
                target.Parts.Add(part.Element);
                continue;
            }

            var coverage = new Coverage { FullyQualifiedClassName = part.Outer, FilePath = part.FilePath, MethodName = part.MethodName, FromGeneratedTypes = true };
            // Every state machine class is a method of its own (overloads); holders of one method share an entry.
            var key = (part.Outer, part.FilePath, part.MethodName, part.StateMachineClass);
            if (standalone.TryGetValue(key, out Target? shared))
            {
                shared.Parts.Add(part.Element);
                continue;
            }

            standalone[key] = Add(targets, new Target(coverage, null, [part.Element]));

            coverageList.Add(coverage);
        }

        foreach (Target target in targets)
        {
            Apply(target);
        }
    }

    private static Target Add(List<Target> targets, Target target)
    {
        targets.Add(target);
        return target;
    }

    private static void Apply(Target target)
    {
        var lines = new SortedDictionary<int, (int Hits, int BranchesCovered, int BranchesTotal)>();
        foreach (XElement element in target.Original is null ? target.Parts : [target.Original, .. target.Parts])
        {
            foreach (XElement line in element.Elements("lines").Elements("line"))
            {
                if (!int.TryParse(line.Attribute("number")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                {
                    continue;
                }

                int.TryParse(line.Attribute("hits")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hits);
                (int covered, int total) = Branches(line);
                lines.TryGetValue(number, out var seen);
                lines[number] = (Math.Max(seen.Hits, hits), Math.Max(seen.BranchesCovered, covered), Math.Max(seen.BranchesTotal, total));
            }
        }

        Coverage coverage = target.Coverage;
        coverage.FromGeneratedTypes = true;
        coverage.LinesTotal = lines.Count;
        coverage.LinesCovered = lines.Values.Count(l => l.Hits > 0);
        coverage.BranchesTotal = lines.Values.Sum(l => l.BranchesTotal);
        coverage.BranchesCovered = lines.Values.Sum(l => l.BranchesCovered);
        coverage.LineRate = null;
        coverage.BranchRate = null;
        coverage.MethodLineNumber = lines.Count > 0 ? lines.Keys.First() : coverage.MethodLineNumber;
        coverage.Complexity = target.Original is null
            ? target.Parts.Sum(p => IntAttribute(p, "complexity"))
            : coverage.Complexity + target.Parts.Sum(p => IntAttribute(p, "complexity"));
    }

    private static (int Covered, int Total) Branches(XElement line)
    {
        Match match = ConditionCoverage().Match(line.Attribute("condition-coverage")?.Value ?? string.Empty);

        return match.Success
            ? (int.Parse(match.Groups["covered"].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups["total"].Value, CultureInfo.InvariantCulture))
            : (0, 0);
    }

    private static int FirstLine(XElement method)
        => int.TryParse(method.Elements("lines").Elements("line").FirstOrDefault()?.Attribute("number")?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;

    private static int IntAttribute(XElement element, string name)
        => int.TryParse(element.Attribute(name)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
}
