/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Xml;
using System.Xml.Linq;

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// Opens coverage files without ever reading anything but the file itself.
/// Cobertura reports of some producers start with a DOCTYPE that names a DTD, so a DOCTYPE is accepted and skipped:
/// no DTD and no external entity is loaded, and entities declared in it are not expanded, so a reference to one
/// is a well-formedness error. The entity limit is a second line of defence should that ever change.
/// </summary>
internal static class CoverageXml
{
    private const long MaxCharactersFromEntities = 1024;

    internal static XmlReader CreateReader(string filePath) => XmlReader.Create(filePath, new XmlReaderSettings
    {
        DtdProcessing = DtdProcessing.Ignore,
        XmlResolver = null,
        MaxCharactersFromEntities = MaxCharactersFromEntities,
    });

    /// <exception cref="InvalidCoverageFileException">The file is not well-formed XML.</exception>
    internal static XDocument Load(string filePath)
    {
        try
        {
            using XmlReader reader = CreateReader(filePath);
            return XDocument.Load(reader);
        }
        catch (XmlException exception)
        {
            throw new InvalidCoverageFileException(filePath, exception.Message, exception);
        }
    }
}
