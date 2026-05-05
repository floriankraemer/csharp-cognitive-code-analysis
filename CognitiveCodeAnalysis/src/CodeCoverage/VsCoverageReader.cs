/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Xml;

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// Reads Visual Studio coverage XML (root element <c>results</c>) with per-module
/// <c>range</c> / <c>source_file</c> data. Uses streaming <see cref="XmlReader"/> for large files.
/// Emits one file-level aggregate <see cref="Coverage"/> per source file (empty FQCN).
/// </summary>
public sealed class VsCoverageReader : ICoverageReader
{
    /// <inheritdoc />
    public IEnumerable<Coverage> ReadCoverage(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Coverage file not found: {filePath}", filePath);
        }

        var fileLines = new Dictionary<string, (HashSet<int> Covered, HashSet<int> All)>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var reader = XmlReader.Create(filePath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });

            var foundRoot = false;
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    continue;
                }

                foundRoot = true;
                if (reader.Name != "results")
                {
                    throw new InvalidOperationException(
                        $"Invalid Visual Studio coverage XML: expected root 'results' but found '{reader.Name}' in {filePath}");
                }

                break;
            }

            if (!foundRoot)
            {
                throw new InvalidOperationException(
                    $"Invalid Visual Studio coverage XML: root 'results' element not found in {filePath}");
            }

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.Name != "module")
                {
                    continue;
                }

                ParseModule(reader, fileLines);
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load or parse Visual Studio coverage XML: {filePath}", ex);
        }

        List<Coverage> list = [];
        foreach (var kvp in fileLines)
        {
            string rawPath = kvp.Key;
            HashSet<int> covered = kvp.Value.Covered;
            HashSet<int> all = kvp.Value.All;

            if (all.Count == 0)
            {
                continue;
            }

            string normalizedPath = NormalizeFilePath(rawPath);
            list.Add(new Coverage
            {
                FullyQualifiedClassName = string.Empty,
                FilePath = normalizedPath,
                MethodName = string.Empty,
                MethodLineNumber = 0,
                LinesCovered = covered.Count,
                LinesTotal = all.Count,
                BranchesCovered = 0,
                BranchesTotal = 0,
                Complexity = 0
            });
        }

        return list;
    }

    private static string NormalizeFilePath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
        }
        catch
        {
            return path;
        }
    }

    private static void ParseModule(XmlReader reader, Dictionary<string, (HashSet<int> Covered, HashSet<int> All)> fileLines)
    {
        var rangesBySourceId = new Dictionary<string, List<(int Line, bool Covered)>>(StringComparer.Ordinal);
        var sourceIdToPath = new Dictionary<string, string>(StringComparer.Ordinal);

        if (reader.IsEmptyElement)
        {
            return;
        }

        int moduleDepth = reader.Depth;

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == moduleDepth)
            {
                break;
            }

            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            switch (reader.Name)
            {
                case "range":
                    ParseRange(reader, rangesBySourceId);
                    break;
                case "source_file":
                    ParseSourceFile(reader, sourceIdToPath);
                    break;
            }
        }

        foreach (var kvp in rangesBySourceId)
        {
            string sourceId = kvp.Key;
            List<(int Line, bool Covered)> ranges = kvp.Value;

            if (!sourceIdToPath.TryGetValue(sourceId, out string? filePath))
            {
                continue;
            }

            if (!fileLines.TryGetValue(filePath, out var entry))
            {
                entry = (new HashSet<int>(), new HashSet<int>());
                fileLines[filePath] = entry;
            }

            foreach (var range in ranges)
            {
                int line = range.Line;
                bool covered = range.Covered;

                entry.All.Add(line);
                if (covered)
                {
                    entry.Covered.Add(line);
                }
            }
        }
    }

    private static void ParseRange(XmlReader reader, Dictionary<string, List<(int Line, bool Covered)>> rangesBySourceId)
    {
        string? sourceId = reader.GetAttribute("source_id");
        string? startLineStr = reader.GetAttribute("start_line");
        string? endLineStr = reader.GetAttribute("end_line");
        string? coveredStr = reader.GetAttribute("covered");

        if (sourceId is null || startLineStr is null || coveredStr is null)
        {
            return;
        }

        if (!int.TryParse(startLineStr, out int startLine))
        {
            return;
        }

        int endLine = startLine;
        if (endLineStr is not null)
        {
            int.TryParse(endLineStr, out endLine);
        }

        bool isCovered = coveredStr.Equals("yes", StringComparison.OrdinalIgnoreCase);

        if (!rangesBySourceId.TryGetValue(sourceId, out var list))
        {
            list = [];
            rangesBySourceId[sourceId] = list;
        }

        for (int line = startLine; line <= endLine; line++)
        {
            list.Add((line, isCovered));
        }
    }

    private static void ParseSourceFile(XmlReader reader, Dictionary<string, string> sourceIdToPath)
    {
        string? id = reader.GetAttribute("id");
        string? path = reader.GetAttribute("path");

        if (id is not null && path is not null)
        {
            sourceIdToPath[id] = path;
        }
    }
}
