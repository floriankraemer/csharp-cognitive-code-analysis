/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text;

namespace CognitiveCodeAnalysis.Churn;

/// <summary>Writes a report so that readers never see a half-written file: temp file in the same directory, then move.</summary>
public static class ChurnReportFileWriter
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static void Write(string path, string content)
    {
        string fullPath = Path.GetFullPath(path);
        string directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);

        string temporary = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, content, Utf8NoBom);
            File.Move(temporary, fullPath, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }
    }
}
