/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

/// <summary>Serializes the XML reports the same way on every platform: UTF-8 without a BOM, two spaces, LF.</summary>
internal static class CognitiveCiXml
{
    internal static string Serialize(XElement root)
    {
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            NewLineHandling = NewLineHandling.Entitize,
        };

        using var stream = new MemoryStream();
        using (XmlWriter writer = XmlWriter.Create(stream, settings))
        {
            document.Save(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray()).TrimEnd('\n') + "\n";
    }
}
