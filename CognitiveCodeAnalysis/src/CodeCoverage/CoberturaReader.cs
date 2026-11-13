/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Xml.Linq;

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// <![CDATA[Reads code coverage data from Cobertura XML format reports.]]>
/// </summary>
public class CoberturaReader : ICoverageReader
{
    /// <summary>
    /// <![CDATA[
    /// Reads coverage data from a Cobertura XML report file.
    /// Two flavours are understood per class and method element:
    /// reports with counts (lines-covered, lines-valid, branches-covered, branches-valid) and
    /// reports with rates only (line-rate, branch-rate), such as the ones coverlet writes.
    /// Counts win when their *-valid attribute is present.
    /// ]]>
    /// </summary>
    /// <param name="filePath">Path to the Cobertura XML file.</param>
    /// <returns>A collection of Coverage objects containing metrics for classes and methods.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the XML file is invalid or malformed.</exception>
    public IEnumerable<Coverage> ReadCoverage(string filePath)
    {
        XElement coverageElement = LoadXmlData(filePath);

        List<string> sourcePaths = GetSourcePaths(coverageElement);
        string? baseSourcePath = sourcePaths.FirstOrDefault();

        List<Coverage> coverageList = [];

        // Process all packages
        IEnumerable<XElement> packages = coverageElement.Elements("packages")?.Elements("package") ?? [];
        foreach (XElement package in packages)
        {
            string packageName = package.Attribute("name")?.Value ?? string.Empty;

            // Process all classes in the package
            IEnumerable<XElement> classes = package.Elements("classes")?.Elements("class") ?? [];
            foreach (XElement classElement in classes)
            {
                string className = classElement.Attribute("name")?.Value ?? string.Empty;
                string fileName = classElement.Attribute("filename")?.Value ?? string.Empty;
                // Coverlet names the package after the assembly and the class after its full name.
                string fullyQualifiedClassName = string.IsNullOrEmpty(packageName)
                    || className.StartsWith(packageName + ".", StringComparison.Ordinal)
                    ? className
                    : $"{packageName}.{className}";

                string absoluteFilePath = ConstructAbsolutePath(fileName, baseSourcePath);

                AddClassLevelCoverage(classElement, coverageList, fullyQualifiedClassName, absoluteFilePath);

                IEnumerable<XElement> methods = classElement.Elements("methods")?.Elements("method") ?? [];
                foreach (XElement methodElement in methods)
                {
                    AddMethodLevelCoverage(methodElement, coverageList, fullyQualifiedClassName, absoluteFilePath);
                }
            }
        }

        return coverageList;
    }

    private static void AddClassLevelCoverage(XElement classElement, List<Coverage> coverageList, string fullyQualifiedClassName,
        string absoluteFilePath)
    {
        var coverage = new Coverage
        {
            FullyQualifiedClassName = fullyQualifiedClassName,
            FilePath = absoluteFilePath,
            MethodName = string.Empty,
            MethodLineNumber = 0,
        };
        ReadFigures(classElement, coverage);
        coverageList.Add(coverage);
    }

    private static void AddMethodLevelCoverage(
        XElement methodElement,
        List<Coverage> coverageList,
        string fullyQualifiedClassName,
        string absoluteFilePath
    ) {
        var coverage = new Coverage
        {
            FullyQualifiedClassName = fullyQualifiedClassName,
            FilePath = absoluteFilePath,
            MethodName = methodElement.Attribute("name")?.Value ?? string.Empty,
            MethodLineNumber = GetMethodLineNumber(methodElement),
        };
        ReadFigures(methodElement, coverage);
        coverageList.Add(coverage);
    }

    private static void ReadFigures(XElement element, Coverage coverage)
    {
        coverage.Complexity = ParseIntAttribute(element, "complexity", 0);

        if (element.Attribute("lines-valid") != null)
        {
            coverage.LinesCovered = ParseIntAttribute(element, "lines-covered", 0);
            coverage.LinesTotal = ParseIntAttribute(element, "lines-valid", 0);
        }
        else
        {
            coverage.LineRate = ParseRateAttribute(element, "line-rate");
        }

        if (element.Attribute("branches-valid") != null)
        {
            coverage.BranchesCovered = ParseIntAttribute(element, "branches-covered", 0);
            coverage.BranchesTotal = ParseIntAttribute(element, "branches-valid", 0);
        }
        else if (HasBranchLines(element))
        {
            coverage.BranchRate = ParseRateAttribute(element, "branch-rate");
        }
    }

    /// <summary>
    /// <![CDATA[
    /// Writers like coverlet report branch-rate="1" for code without any branch.
    /// When the element lists its lines, only a line flagged as a branch proves that branch data exists.
    /// An element without a lines list is trusted as is.
    /// ]]>
    /// </summary>
    private static bool HasBranchLines(XElement element)
    {
        XElement? lines = element.Element("lines");

        return lines == null || lines.Elements("line").Any(line =>
            string.Equals(line.Attribute("branch")?.Value, "true", StringComparison.OrdinalIgnoreCase));
    }

    private static double? ParseRateAttribute(XElement element, string attributeName)
    {
        string? value = element.Attribute(attributeName)?.Value;

        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double rate)
            ? Math.Clamp(rate, 0.0, 1.0)
            : null;
    }

    /// <summary>
    /// <![CDATA[Gets the source paths from the coverage XML element.]]>
    /// </summary>
    private static List<string> GetSourcePaths(XElement coverageElement)
    {
        List<string> sourcePaths = [];
        XElement? sourcesElement = coverageElement.Element("sources");

        if (sourcesElement == null)
        {
            return sourcePaths;
        }

        foreach (XElement sourceElement in sourcesElement.Elements("source"))
        {
            string sourcePath = sourceElement.Value;
            if (!string.IsNullOrEmpty(sourcePath))
            {
                sourcePaths.Add(sourcePath);
            }
        }

        return sourcePaths;
    }

    /// <summary>
    /// <![CDATA[Constructs an absolute file path from a relative filename and base source path.]]>
    /// </summary>
    private static string ConstructAbsolutePath(string fileName, string? baseSourcePath)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }

        if (Path.IsPathRooted(fileName))
        {
            return Path.GetFullPath(fileName);
        }

        // If we have a base source path, combine it with the filename
        if (!string.IsNullOrEmpty(baseSourcePath))
        {
            try
            {
                // Normalize the base source path
                string normalizedBase = Path.GetFullPath(baseSourcePath);
                return Path.GetFullPath(Path.Combine(normalizedBase, fileName));
            }
            catch
            {
                // If path combination fails, try to use the filename as-is
            }
        }

        // Fallback: try to resolve relative to current directory
        try
        {
            return Path.GetFullPath(fileName);
        }
        catch
        {
            return fileName;
        }
    }

    /// <summary>
    /// <![CDATA[Parses an integer attribute from an XML element, returning a default value if not found or invalid.]]>
    /// </summary>
    private static int ParseIntAttribute(XElement element, string attributeName, int defaultValue)
    {
        string? value = element.Attribute(attributeName)?.Value;
        if (string.IsNullOrEmpty(value))
        {
            return defaultValue;
        }

        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result) ? result : defaultValue;
    }

    /// <summary>
    /// <![CDATA[Gets the line number where a method starts by examining the lines element.]]>
    /// </summary>
    private static int GetMethodLineNumber(XElement methodElement)
    {
        XElement? linesElement = methodElement.Element("lines");
        if (linesElement == null)
        {
            return 0;
        }

        // Get the first line element to determine the method's starting line
        XElement? firstLine = linesElement.Elements("line").FirstOrDefault();
        if (firstLine == null)
        {
            return 0;
        }

        string? lineNumberStr = firstLine.Attribute("number")?.Value;
        if (string.IsNullOrEmpty(lineNumberStr))
        {
            return 0;
        }

        return int.TryParse(lineNumberStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int lineNumber) ? lineNumber : 0;
    }

    private XElement LoadXmlData(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Coverage file not found: {filePath}", filePath);
        }

        XDocument document;
        try
        {
            document = XDocument.Load(filePath);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load or parse XML file: {filePath}", ex);
        }

        XElement? coverageElement = document.Element("coverage");
        if (coverageElement == null)
        {
            throw new InvalidOperationException($"Invalid Cobertura XML format: root 'coverage' element not found in {filePath}");
        }

        return coverageElement;
    }
}
