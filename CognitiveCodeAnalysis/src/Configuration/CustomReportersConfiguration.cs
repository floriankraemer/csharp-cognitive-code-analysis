/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Configuration;

/// <summary>Where a custom reporter lives: the type name and the plugin assembly it is in.</summary>
public sealed class CustomReporterEntry
{
    /// <summary>The full name of the class, such as <c>Acme.HotspotReporter</c>.</summary>
    public string? Type { get; set; }

    /// <summary>The plugin assembly. A relative path is resolved against the directory of the configuration file.</summary>
    public string? Assembly { get; set; }
}

/// <summary>The <c>customReporters</c> section of the configuration: the reporters by the key given to <c>-r</c>.</summary>
public sealed class CustomReportersConfiguration
{
    /// <summary>Reporters for the <c>analyse</c> command.</summary>
    public Dictionary<string, CustomReporterEntry> Cognitive { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Reporters for the <c>churn</c> command.</summary>
    public Dictionary<string, CustomReporterEntry> Churn { get; set; } = new(StringComparer.Ordinal);
}
