/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

internal sealed class AnalysisProgressCollector : IProgress<AnalysisProgress>
{
    private readonly List<AnalysisProgress> _reports = [];
    private readonly object _lock = new();

    public IReadOnlyList<AnalysisProgress> Reports
    {
        get
        {
            lock (_lock)
            {
                return _reports.ToList();
            }
        }
    }

    public void Report(AnalysisProgress value)
    {
        lock (_lock)
        {
            _reports.Add(value);
        }
    }
}
