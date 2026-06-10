/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

public static class BaselineMethodKey
{
    public static string FromMetrics(CognitiveMetrics metrics) =>
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
