/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.IO.Enumeration;
using System.Text.RegularExpressions;

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

/// <summary>
/// <![CDATA[Finds the C# source files of a list of files and directories.]]>
/// </summary>
public class SourceFileFinder
{
    private const string SourceFileExtension = ".cs";

    // Build output holds generated code; dot directories hold tool state such as .git and .vs.
    private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.Ordinal) { "bin", "obj" };

    public List<string> FindSourceFiles(string[] directories)
        => FindSourceFiles(directories, progress: null);

    public List<string> FindSourceFiles(string[] directories, IProgress<AnalysisProgress>? progress)
        => FindSourceFiles(directories, excludeFilePatterns: [], progress);

    /// <summary>
    /// <![CDATA[
    /// Returns the C# files among <paramref name="paths"/> and below its directories, absolute, sorted and without duplicates.
    /// Missing paths are skipped. Directories named bin or obj and directories whose name starts with a dot are not searched.
    /// A file is left out when one of <paramref name="excludeFilePatterns"/> matches its absolute path written with '/'.
    /// ]]>
    /// </summary>
    public List<string> FindSourceFiles(
        string[] paths,
        IReadOnlyCollection<string> excludeFilePatterns,
        IProgress<AnalysisProgress>? progress
    ) {
        progress?.Report(new AnalysisProgress(AnalysisProgressPhase.SearchingFiles));

        Regex[] excludes = RegexPatterns.Compile(excludeFilePatterns);
        List<string> files = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .SelectMany(FilesOf)
            .Where(file => !excludes.Any(pattern => pattern.IsMatch(file.Replace('\\', '/'))))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        progress?.Report(new AnalysisProgress(AnalysisProgressPhase.SearchCompleted, TotalFiles: files.Count));

        return files;
    }

    /// <summary>
    /// <![CDATA[
    /// Splits the search path argument into absolute paths: a comma-separated list of files and directories,
    /// trimmed, without duplicates and sorted. A path that exists as written stays one path even when it contains a comma.
    /// ]]>
    /// </summary>
    public static string[] SplitPathList(string pathList)
    {
        string[] parts = File.Exists(pathList) || Directory.Exists(pathList)
            ? [pathList]
            : pathList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        return parts.Length == 0
            ? [Path.GetFullPath(pathList)]
            : parts.Select(Path.GetFullPath).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path.Trim().Trim('"', '\''));
    }

    private static IEnumerable<string> FilesOf(string path)
    {
        if (File.Exists(path))
        {
            return IsSourceFile(path) ? [path] : [];
        }

        if (!Directory.Exists(path))
        {
            return [];
        }

        return new FileSystemEnumerable<string>(
            path,
            (ref FileSystemEntry entry) => entry.ToFullPath(),
            new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = 0 })
        {
            ShouldIncludePredicate = (ref FileSystemEntry entry) => !entry.IsDirectory && IsSourceFile(entry.FileName),
            ShouldRecursePredicate = (ref FileSystemEntry entry) => !IsSkippedDirectory(entry.FileName),
        };
    }

    private static bool IsSourceFile(ReadOnlySpan<char> fileName) =>
        fileName.EndsWith(SourceFileExtension, StringComparison.OrdinalIgnoreCase);

    private static bool IsSkippedDirectory(ReadOnlySpan<char> name) =>
        name.StartsWith(".", StringComparison.Ordinal) || SkippedDirectoryNames.Contains(name.ToString());
}
