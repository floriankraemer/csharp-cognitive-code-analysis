/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Application;

public interface IAnalysisTracer
{
    void Trace(string message);

    void TraceStep(string stepName, Action action);

    T TraceStep<T>(string stepName, Func<T> action);
}

public sealed class NullAnalysisTracer : IAnalysisTracer
{
    public static readonly NullAnalysisTracer Instance = new();

    private NullAnalysisTracer()
    {
    }

    public void Trace(string message)
    {
    }

    public void TraceStep(string stepName, Action action) => action();

    public T TraceStep<T>(string stepName, Func<T> action) => action();
}
