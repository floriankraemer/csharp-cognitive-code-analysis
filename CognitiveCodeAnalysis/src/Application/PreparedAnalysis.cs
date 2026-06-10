/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Application;

public sealed record PreparedAnalysis(
    CognitiveConfiguration Configuration,
    string AbsoluteSourcePath,
    string ReportType,
    string OutputFile,
    string? BaselineFile,
    string? CoverageCobertura,
    bool IsConsoleTextReport
);
