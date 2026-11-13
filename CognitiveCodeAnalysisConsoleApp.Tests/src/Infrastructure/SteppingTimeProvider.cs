/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysisConsoleApp.Tests.Infrastructure;

/// <summary>A clock that moves by a fixed step each time it is read, so measured durations are exact.</summary>
internal sealed class SteppingTimeProvider(long stepMilliseconds = 100) : TimeProvider
{
    private long _milliseconds;

    public override long TimestampFrequency => 1000;

    public override long GetTimestamp() => _milliseconds += stepMilliseconds;
}
