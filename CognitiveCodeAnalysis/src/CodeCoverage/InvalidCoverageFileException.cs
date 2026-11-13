/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CodeCoverage;

/// <summary>
/// A coverage file that is not well-formed XML or not a report of the expected format.
/// The message names the file and the reason and is safe to show to the user.
/// </summary>
public sealed class InvalidCoverageFileException(string filePath, string reason, Exception? inner = null)
    : InvalidOperationException($"Invalid coverage file {filePath}: {reason}", inner);
