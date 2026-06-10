/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysisConsoleApp.Progress;

namespace CognitiveCodeAnalysisConsoleApp.Application;

internal interface IReportGenerationService
{
    void GenerateReport(
        PreparedAnalysis prepared,
        CognitiveMetricsCollection metricsCollection,
        CognitiveBaselineComparison? baselineComparison,
        IProgress<AnalysisProgress>? progress = null,
        SpectreAnalysisProgressReporter? progressReporter = null
    );
}
