/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

public static class BaselineMethodKey
{
    /// <summary>The key of a current method: the path relative to the working directory, so another checkout path still matches.</summary>
    public static string FromMetrics(CognitiveMetrics metrics) =>
        Build(CognitiveCiLocation.DisplayPath(metrics.FilePath), metrics.ClassName, metrics.methodSignature);

    /// <summary>The key a baseline written before relative paths used for the method, with its absolute path.</summary>
    internal static string AbsoluteFromMetrics(CognitiveMetrics metrics) =>
        Build(metrics.FilePath, metrics.ClassName, metrics.methodSignature);

    public static string FromSnapshot(CognitiveBaselineMethodSnapshot snapshot) =>
        Build(snapshot.FilePath, snapshot.ClassName, snapshot.MethodSignature);

    private static string Build(string filePath, string className, string methodSignature) =>
        CognitiveCiEncoding.NormalizeFilePath(filePath)
        + "|"
        + className
        + "|"
        + methodSignature;
}
