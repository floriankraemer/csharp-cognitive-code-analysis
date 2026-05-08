/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

internal static class CognitiveCiEncoding
{
    internal static string EncodeWorkflowCommandMessage(string s) =>
        s.Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);

    internal static string NormalizeFilePath(string path) =>
        path.Replace("\\", "/", StringComparison.Ordinal);
}
