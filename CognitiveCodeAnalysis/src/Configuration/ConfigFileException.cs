/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Configuration;

/// <summary>A failure to create a configuration file that the user can act on, e.g. the file already exists.</summary>
public sealed class ConfigFileException : Exception
{
    public ConfigFileException(string message) : base(message)
    {
    }

    public ConfigFileException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
