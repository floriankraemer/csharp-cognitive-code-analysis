/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Configuration;

/// <summary>
/// The configuration is readable but its content cannot be used.
/// </summary>
public sealed class ConfigurationException(string message) : InvalidOperationException(message);
