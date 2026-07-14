/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysisConsoleApp.Commands;

namespace CognitiveCodeAnalysisConsoleApp.Application;

internal static class AnalyseRequestMapper
{
    public static AnalysisRequest FromSettings(AnalyseCommandSettings settings)
    {
        AnalysisDisplayOverrides? displayOverrides = null;

        if (settings.ShowHalstead.HasValue
            || settings.ShowCyclomatic.HasValue
            || settings.ShowCoupling.HasValue)
        {
            displayOverrides = new AnalysisDisplayOverrides(
                ShowHalstead: settings.ShowHalstead,
                ShowCyclomatic: settings.ShowCyclomatic,
                ShowCoupling: settings.ShowCoupling
            );
        }

        return new AnalysisRequest(
            SourcePath: settings.SourcePath,
            ConfigFile: settings.ConfigFile,
            ReportType: settings.ReportType ?? "ConsoleText",
            BaselineFile: settings.BaselineFile,
            OutputFile: settings.OutputFile,
            CoverageCobertura: settings.CoverageCobertura,
            DisplayOverrides: displayOverrides,
            Verbose: settings.Verbose
        );
    }
}
