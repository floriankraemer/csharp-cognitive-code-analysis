using System.Xml.Linq;

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// <![CDATA[Reads code coverage data from Cobertura XML format reports.]]>
/// </summary>
public class CoberturaReader : ICoverageReader
{
    /// <summary>
    /// <![CDATA[Reads coverage data from a Cobertura XML report file.]]>
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
                string fullyQualifiedClassName = string.IsNullOrEmpty(packageName)
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
        int classLinesCovered = ParseIntAttribute(classElement, "lines-covered", 0);
        int classLinesTotal = ParseIntAttribute(classElement, "lines-valid", 0);
        int classBranchesCovered = ParseIntAttribute(classElement, "branches-covered", 0);
        int classBranchesTotal = ParseIntAttribute(classElement, "branches-valid", 0);
        int classComplexity = ParseIntAttribute(classElement, "complexity", 0);

        coverageList.Add(new Coverage
        {
            FullyQualifiedClassName = fullyQualifiedClassName,
            FilePath = absoluteFilePath,
            MethodName = string.Empty,
            MethodLineNumber = 0,
            LinesCovered = classLinesCovered,
            LinesTotal = classLinesTotal,
            BranchesCovered = classBranchesCovered,
            BranchesTotal = classBranchesTotal,
            Complexity = classComplexity
        });
    }

    private static void AddMethodLevelCoverage(
        XElement methodElement,
        List<Coverage> coverageList,
        string fullyQualifiedClassName,
        string absoluteFilePath
    ) {
        coverageList.Add(new Coverage
        {
            FullyQualifiedClassName = fullyQualifiedClassName,
            FilePath = absoluteFilePath,
            MethodName = methodElement.Attribute("name")?.Value ?? string.Empty,
            MethodLineNumber = GetMethodLineNumber(methodElement),
            LinesCovered = ParseIntAttribute(methodElement, "lines-covered", 0),
            LinesTotal = ParseIntAttribute(methodElement, "lines-valid", 0),
            BranchesCovered = ParseIntAttribute(methodElement, "branches-covered", 0),
            BranchesTotal = ParseIntAttribute(methodElement, "branches-valid", 0),
            Complexity = ParseIntAttribute(methodElement, "complexity", 0)
        });
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

        return int.TryParse(value, out int result) ? result : defaultValue;
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

        return int.TryParse(lineNumberStr, out int lineNumber) ? lineNumber : 0;
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
