/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Application;

public sealed record CoverageApplicationResult(bool Success, string? WarningMessage = null);
