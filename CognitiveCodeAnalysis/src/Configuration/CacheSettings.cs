/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Configuration;

/// <summary>
/// The analysis cache. Off unless enabled here or with <c>--cache</c>, so a run creates no directory by default.
/// </summary>
public sealed class CacheSettings
{
    public const string DefaultDirectory = ".cca/cache";

    public bool Enabled { get; set; }

    /// <summary>Resolved against the working directory when relative.</summary>
    public string Directory { get; set; } = DefaultDirectory;
}
