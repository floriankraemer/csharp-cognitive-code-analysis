/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysisConsoleApp.Progress;

public sealed class SpectreAnalysisProgressState
{
    public bool SearchStarted { get; internal set; }
    public bool SearchCompleted { get; internal set; }
    public int FoundFileCount { get; internal set; }
    public string? SearchCompleteMessage { get; internal set; }
    public string? AnalysisDescription { get; internal set; }
    public double AnalysisValue { get; internal set; }
    public double AnalysisMaxValue { get; internal set; }
    public bool AnalysisCompleted { get; internal set; }
}
