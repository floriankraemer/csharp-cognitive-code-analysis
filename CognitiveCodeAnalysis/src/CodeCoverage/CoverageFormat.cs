/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// The coverage format a user selected. <see cref="Auto"/> is what <c>--coverage-cobertura</c> has always done:
/// Cobertura and Visual Studio XML are told apart by their root element.
/// Clover shares the root element with Cobertura, so it has to be asked for.
/// </summary>
public enum CoverageFormat
{
    Auto,
    Clover,
}

public static class CoverageFormatExtensions
{
    /// <summary>The reader for the format; <paramref name="autoDetect"/> serves <see cref="CoverageFormat.Auto"/>.</summary>
    public static ICoverageReader ReaderFor(this CoverageFormat format, ICoverageReader autoDetect) =>
        format == CoverageFormat.Clover ? new CloverReader() : autoDetect;
}
