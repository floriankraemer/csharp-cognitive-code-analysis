/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysisConsoleApp.Infrastructure;

internal interface IConsoleNotifier
{
    void WriteError(string message);

    void WriteWarning(string message);

    void WriteNoSourceFilesFound(string absoluteSourcePath);

    void WriteReportGenerated(string reportType, string fullPath);

    void WriteConfigUsed(string configSourceDisplay);

    void WriteConfigFileCreated(string fullPath);
}
