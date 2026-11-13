/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Reporting;

/// <summary>A custom reporter cannot be found, loaded, created or run. The message says which and why.</summary>
public sealed class CustomReporterException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
