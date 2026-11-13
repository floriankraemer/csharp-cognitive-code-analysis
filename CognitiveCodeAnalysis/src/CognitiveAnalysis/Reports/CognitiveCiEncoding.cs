/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

internal static class CognitiveCiEncoding
{
    /// <summary>Escapes the data of a GitHub Actions workflow command, which is the message after <c>::</c>.</summary>
    internal static string EncodeWorkflowCommandMessage(string s) =>
        s.Replace("%", "%25", StringComparison.Ordinal)
            .Replace("\r", "%0D", StringComparison.Ordinal)
            .Replace("\n", "%0A", StringComparison.Ordinal);

    internal static string NormalizeFilePath(string path) =>
        path.Replace("\\", "/", StringComparison.Ordinal);

    /// <summary>Escapes a property value of a workflow command, where <c>:</c> and <c>,</c> are separators as well.</summary>
    internal static string EncodeWorkflowProperty(string s) =>
        EncodeWorkflowCommandMessage(s)
            .Replace(":", "%3A", StringComparison.Ordinal)
            .Replace(",", "%2C", StringComparison.Ordinal);
}
