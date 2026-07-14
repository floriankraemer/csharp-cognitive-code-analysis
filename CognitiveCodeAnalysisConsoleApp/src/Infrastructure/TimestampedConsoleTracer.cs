/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Diagnostics;
using System.Globalization;

using CognitiveCodeAnalysis.Application;

namespace CognitiveCodeAnalysisConsoleApp.Infrastructure;

internal sealed class TimestampedConsoleTracer : IAnalysisTracer
{
    public void Trace(string message)
    {
        string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
        Console.Error.WriteLine($"[{timestamp}] {message}");
    }

    public void TraceStep(string stepName, Action action)
    {
        Trace($"{stepName}...");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            action();
        }
        finally
        {
            stopwatch.Stop();
            Trace($"{stepName} done ({stopwatch.ElapsedMilliseconds} ms)");
        }
    }

    public T TraceStep<T>(string stepName, Func<T> action)
    {
        Trace($"{stepName}...");
        var stopwatch = Stopwatch.StartNew();
        try
        {
            return action();
        }
        finally
        {
            stopwatch.Stop();
            Trace($"{stepName} done ({stopwatch.ElapsedMilliseconds} ms)");
        }
    }
}
