/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Churn;

/// <summary>A failure of the churn calculation that the user can act on, e.g. git is missing.</summary>
public sealed class ChurnException : Exception
{
    public ChurnException(string message) : base(message)
    {
    }

    public ChurnException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
