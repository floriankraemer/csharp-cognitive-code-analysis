/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Configuration;

public class MetricConfiguration
{
    public int Threshold { get; set; }
    public double Scale { get; set; }
    public bool Enabled { get; set; }
}
