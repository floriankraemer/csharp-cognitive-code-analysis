/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysisConsoleApp.Commands;

internal static class CoverageOptions
{
    public const string OnlyOneFormatMessage = "Only one coverage format can be specified at a time.";

    public static bool AreExclusive(string? cobertura, string? clover) =>
        string.IsNullOrWhiteSpace(cobertura) || string.IsNullOrWhiteSpace(clover);
}
