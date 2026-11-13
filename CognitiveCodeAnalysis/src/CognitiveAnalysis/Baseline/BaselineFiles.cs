/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

/// <summary>
/// The baseline files in <c>.cca/baseline</c> below the working directory. Names and timestamps use UTC, so a change
/// of time zone cannot reorder them.
/// </summary>
public static partial class BaselineFiles
{
    /// <summary>The directory of the baseline files, relative to the working directory.</summary>
    public static readonly string RelativeDirectory = Path.Combine(".cca", "baseline");

    private const string TimestampFormat = "yyyy-MM-dd_HH-mm-ss";

    [GeneratedRegex(@"^baseline-(\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2})(?:-(\d+))?\.json$", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();

    /// <summary>
    /// The newest regular file <c>baseline-&lt;timestamp&gt;[-&lt;n&gt;].json</c>, by the timestamp of its name and then
    /// its suffix. Other names, including impossible dates, are not baselines.
    /// </summary>
    /// <returns>The full path, or null when the directory does not exist, holds no baseline or cannot be read.</returns>
    public static string? Latest(string workingDirectory) => Latest(workingDirectory, out _);

    /// <param name="warning">Why the directory could not be read, otherwise null.</param>
    public static string? Latest(string workingDirectory, out string? warning)
    {
        warning = null;
        string directory = Path.Combine(workingDirectory, RelativeDirectory);
        try
        {
            if (!Directory.Exists(directory))
            {
                return null;
            }

            return Directory.EnumerateFiles(directory)
                .Select(file => (File: file, Key: SortKey(Path.GetFileName(file))))
                .Where(candidate => candidate.Key != null)
                .OrderBy(candidate => candidate.Key!.Value.Timestamp)
                .ThenBy(candidate => candidate.Key!.Value.Suffix)
                .Select(candidate => candidate.File)
                .LastOrDefault();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            warning = $"Cannot read baseline directory {directory}: {exception.Message}";
            return null;
        }
    }

    private static (DateTime Timestamp, int Suffix)? SortKey(string fileName)
    {
        Match match = FileNamePattern().Match(fileName);
        if (!match.Success
            || !DateTime.TryParseExact(
                match.Groups[1].Value, TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timestamp))
        {
            return null;
        }

        if (!match.Groups[2].Success)
        {
            return (timestamp, 0);
        }

        return int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int suffix)
            ? (timestamp, suffix)
            : null;
    }

    /// <summary>
    /// Writes the snapshot as a new baseline file. The content goes to a temporary file in the directory first and is
    /// then moved into place without replacing anything, so a reader never sees a partial file and an existing file
    /// is never overwritten: when the name is taken, <c>-1</c>, <c>-2</c> and so on are appended.
    /// </summary>
    /// <returns>The path of the new file, relative to the working directory.</returns>
    public static string Generate(string workingDirectory, CognitiveBaselineSnapshot snapshot)
    {
        string stem = "baseline-" + snapshot.GeneratedAt.UtcDateTime.ToString(TimestampFormat, CultureInfo.InvariantCulture);
        string directory = Path.Combine(workingDirectory, RelativeDirectory);
        Directory.CreateDirectory(directory);

        string temporary = Path.Combine(directory, $".{stem}-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, BaselineLoader.Serialize(snapshot) + "\n", new UTF8Encoding(false));
            for (int suffix = 0; ; suffix++)
            {
                string relative = Path.Combine(RelativeDirectory, stem + (suffix == 0 ? "" : "-" + suffix) + ".json");
                if (TryMoveNew(temporary, Path.Combine(workingDirectory, relative)))
                {
                    return relative;
                }
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    private static bool TryMoveNew(string temporary, string target)
    {
        try
        {
            File.Move(temporary, target, overwrite: false);
            return true;
        }
        catch (IOException) when (File.Exists(target) || Directory.Exists(target))
        {
            return false;
        }
    }
}
