/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Application;

public sealed record AnalysisRequest(
    string? SourcePath,
    string? ConfigFile,
    string ReportType,
    string? BaselineFile,
    string? OutputFile,
    string? CoverageCobertura,
    AnalysisDisplayOverrides? DisplayOverrides = null,
    bool Verbose = false
);
