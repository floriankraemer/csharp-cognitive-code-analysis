/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.RegularExpressions;

namespace CognitiveCodeAnalysis.Configuration;

/// <summary>
/// The regular expressions of <c>excludeFilePatterns</c> and <c>excludePatterns</c>.
/// </summary>
public static class RegexPatterns
{
    public static Regex[] Compile(IEnumerable<string> patterns) =>
        patterns.Select(pattern => new Regex(pattern, RegexOptions.CultureInvariant)).ToArray();

    /// <summary>
    /// Drops every pattern that is not a valid regular expression and records a warning for it.
    /// The keys had no effect before they were applied, so an invalid pattern in an old configuration must not fail the run.
    /// </summary>
    internal static string[] KeepValid(string[] patterns, string key, List<string> warnings)
    {
        var valid = new List<string>();
        foreach (string pattern in patterns)
        {
            try
            {
                _ = new Regex(pattern, RegexOptions.CultureInvariant);
                valid.Add(pattern);
            }
            catch (ArgumentException exception)
            {
                warnings.Add($"Invalid regular expression '{pattern}' in '{key}' is ignored: {exception.Message}");
            }
        }

        return [.. valid];
    }
}
