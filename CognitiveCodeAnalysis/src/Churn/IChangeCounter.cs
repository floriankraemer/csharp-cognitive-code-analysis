/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Churn;

/// <summary>Counts how often a file changed in the version history.</summary>
public interface IChangeCounter
{
    int Count(string file);
}
