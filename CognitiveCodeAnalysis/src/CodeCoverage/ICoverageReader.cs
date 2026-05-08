/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// Interface for reading code coverage data from various report formats.
/// </summary>
public interface ICoverageReader
{
    /// <summary>
    /// Reads coverage data from a coverage report file.
    /// </summary>
    /// <param name="filePath">Path to the coverage report file.</param>
    /// <returns>A collection of Coverage objects containing metrics for classes and methods.</returns>
    IEnumerable<Coverage> ReadCoverage(string filePath);
}
