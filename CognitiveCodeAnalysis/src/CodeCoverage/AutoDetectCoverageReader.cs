/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Xml;

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// Dispatches to <see cref="CoberturaReader"/> or <see cref="VsCoverageReader"/> based on the XML root element.
/// </summary>
public sealed class AutoDetectCoverageReader : ICoverageReader
{
    private readonly CoberturaReader _coberturaReader = new();
    private readonly VsCoverageReader _vsCoverageReader = new();

    /// <inheritdoc />
    public IEnumerable<Coverage> ReadCoverage(string filePath)
    {
        string rootName = PeekRootElementName(filePath);

        return rootName switch
        {
            "coverage" => _coberturaReader.ReadCoverage(filePath),
            "results" => _vsCoverageReader.ReadCoverage(filePath),
            _ => throw new InvalidOperationException(
                $"Unrecognized coverage XML format (root element: '{rootName}'). " +
                "Expected Cobertura (<coverage>) or Visual Studio coverage (<results>).")
        };
    }

    private static string PeekRootElementName(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Coverage file not found: {filePath}", filePath);
        }

        using var reader = XmlReader.Create(filePath, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element)
            {
                return reader.Name;
            }
        }

        throw new InvalidOperationException($"Coverage XML file contains no elements: {filePath}");
    }
}
