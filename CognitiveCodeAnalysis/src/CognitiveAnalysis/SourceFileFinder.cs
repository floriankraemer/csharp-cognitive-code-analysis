/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

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

        return FilesBelow(path);
    }

    /// <summary>
    /// <![CDATA[
    /// Searches a directory tree and follows symbolic links, but enters every real directory only once,
    /// so a link to an ancestor neither loops nor reports a file twice.
    /// ]]>
    /// </summary>
    private static IEnumerable<string> FilesBelow(string root)
    {
        var options = new EnumerationOptions { AttributesToSkip = 0, IgnoreInaccessible = false };
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<(string Path, string? RealPath)>();
        pending.Push((root, RealPath(root)));

        while (pending.TryPop(out (string Path, string? RealPath) directory))
        {
            if (directory.RealPath is null || !visited.Add(directory.RealPath))
            {
                continue;
            }

            foreach (string file in Directory.EnumerateFiles(directory.Path, "*", options).Where(file => IsSourceFile(file)))
            {
                yield return file;
            }

            foreach (DirectoryInfo child in new DirectoryInfo(directory.Path).EnumerateDirectories("*", options))
            {
                if (!IsSkippedDirectory(child.Name))
                {
                    string? realPath = child.LinkTarget is null ? Path.Combine(directory.RealPath, child.Name) : RealPath(child.FullName);
                    pending.Push((child.FullName, realPath));
                }
            }
        }
    }

    /// <summary>
    /// <![CDATA[The path with every symbolic link resolved, or null when a link cannot be resolved, for example a link cycle.]]>
    /// </summary>
    private static string? RealPath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? parent = Path.GetDirectoryName(fullPath);
        if (parent is null)
        {
            return fullPath;
        }

        string? realParent = RealPath(parent);
        if (realParent is null)
        {
            return null;
        }

        string candidate = Path.Combine(realParent, Path.GetFileName(fullPath));
        try
        {
            FileSystemInfo? target = new DirectoryInfo(candidate).ResolveLinkTarget(returnFinalTarget: true);
            return target is null ? candidate : RealPath(target.FullName);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IsSourceFile(ReadOnlySpan<char> fileName) =>
        fileName.EndsWith(SourceFileExtension, StringComparison.OrdinalIgnoreCase);

    private static bool IsSkippedDirectory(ReadOnlySpan<char> name) =>
        name.StartsWith(".", StringComparison.Ordinal) || SkippedDirectoryNames.Contains(name.ToString());
}
