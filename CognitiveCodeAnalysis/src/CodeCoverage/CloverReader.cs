/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Xml.Linq;

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// <![CDATA[
/// Reads Clover XML reports (Clover, OpenClover and the PHPUnit flavour).
/// A class carries its statement and conditional counts in <metrics>.
/// The methods of a file are <line type="method"> entries; the stmt lines up to the next method belong to the method.
/// ]]>
/// </summary>
public sealed class CloverReader : ICoverageReader
{
    /// <summary>OpenClover's name for the package of classes without one.</summary>
    private const string DefaultPackage = "default-pkg";

    /// <inheritdoc />
    public IEnumerable<Coverage> ReadCoverage(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Coverage file not found: {filePath}", filePath);
        }

        XElement root = CoverageXml.Load(filePath).Root!;
        if (root.Name.LocalName != "coverage" || !root.Elements("project").Any())
        {
            throw new InvalidCoverageFileException(
                filePath, $"not a Clover report, the root element is <{root.Name.LocalName}>");
        }

        List<Coverage> coverage = [];
        foreach (XElement file in root.Elements("project").Descendants("file"))
        {
            AddFile(file, coverage);
        }

        return coverage;
    }

    private static void AddFile(XElement file, List<Coverage> coverage)
    {
        string filePath = FilePathOf(file);
        string firstClass = string.Empty;

        foreach (XElement type in file.Elements("class"))
        {
            string name = QualifiedName(file, type);
            firstClass = firstClass.Length == 0 ? name : firstClass;
            var entry = new Coverage { FullyQualifiedClassName = name, FilePath = filePath };
            if (type.Element("metrics") is { } metrics)
            {
                ReadMetrics(metrics, entry);
            }

            coverage.Add(entry);
        }

        coverage.AddRange(ReadMethods(file, firstClass, filePath));
    }

    /// <summary>OpenClover names the file by its simple name and gives the location in <c>path</c>; PHPUnit puts it in <c>name</c>.</summary>
    private static string FilePathOf(XElement file)
    {
        string path = file.Attribute("path")?.Value ?? file.Attribute("name")?.Value ?? string.Empty;

        return path.Length == 0 ? path : Path.GetFullPath(path);
    }

    /// <summary>Clover for PHP gives a namespace, OpenClover a package; either is prepended unless the name already has it.</summary>
    private static string QualifiedName(XElement file, XElement type)
    {
        string name = type.Attribute("name")?.Value ?? string.Empty;
        string prefix = (type.Attribute("namespace")?.Value
            ?? (file.Parent is { Name.LocalName: "package" } package ? package.Attribute("name")?.Value : null)
            ?? string.Empty).Replace('\\', '.');

        return prefix.Length == 0
            || prefix == DefaultPackage
            || name.StartsWith(prefix + ".", StringComparison.Ordinal)
            ? name
            : $"{prefix}.{name}";
    }

    private static void ReadMetrics(XElement metrics, Coverage coverage)
    {
        coverage.Complexity = (int)Number(metrics, "complexity");
        coverage.LinesTotal = (int)Number(metrics, "statements");
        coverage.LinesCovered = (int)Number(metrics, "coveredstatements");
        coverage.BranchesTotal = (int)Number(metrics, "conditionals");
        coverage.BranchesCovered = (int)Number(metrics, "coveredconditionals");
    }

    private static IEnumerable<Coverage> ReadMethods(XElement file, string className, string filePath)
    {
        Coverage? current = null;
        double currentCalls = 0;

        foreach (XElement line in file.Elements("line"))
        {
            switch (line.Attribute("type")?.Value)
            {
                case "method":
                    if (current is not null)
                    {
                        yield return Complete(current, currentCalls);
                    }

                    currentCalls = Number(line, "count");
                    current = new Coverage
                    {
                        FullyQualifiedClassName = className,
                        FilePath = filePath,
                        MethodName = MethodName(line),
                        MethodLineNumber = (int)Number(line, "num"),
                        Complexity = (int)Number(line, "complexity"),
                    };
                    break;
                case "stmt" when current is not null:
                    current.LinesTotal++;
                    current.LinesCovered += Number(line, "count") > 0 ? 1 : 0;
                    break;
                case "cond" when current is not null:
                    current.BranchesTotal += 2;
                    current.BranchesCovered += (Number(line, "truecount") > 0 ? 1 : 0) + (Number(line, "falsecount") > 0 ? 1 : 0);
                    break;
            }
        }

        if (current is not null)
        {
            yield return Complete(current, currentCalls);
        }
    }

    /// <summary>A method without statements is as covered as it is called.</summary>
    private static Coverage Complete(Coverage method, double calls)
    {
        if (method.LinesTotal == 0)
        {
            method.LineRate = calls > 0 ? 1.0 : 0.0;
        }

        return method;
    }

    /// <summary>The <c>name</c> of Clover for PHP, or the <c>signature</c> of OpenClover without its parameters.</summary>
    private static string MethodName(XElement method)
    {
        string name = method.Attribute("name")?.Value ?? method.Attribute("signature")?.Value ?? string.Empty;
        int parameters = name.IndexOf('(');

        return (parameters < 0 ? name : name[..parameters]).Trim();
    }

    private static double Number(XElement element, string attribute) =>
        double.TryParse(element.Attribute(attribute)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : 0;
}
