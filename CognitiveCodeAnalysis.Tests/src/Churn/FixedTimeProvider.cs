/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Tests.Churn;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static readonly FixedTimeProvider Default = new(new DateTimeOffset(2023, 10, 1, 12, 0, 0, TimeSpan.Zero));

    public override DateTimeOffset GetUtcNow() => now;

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}
