/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

/// <summary>
/// Locations as the CI formats express them: a line that is never before the first line, paths relative to the
/// working directory with <c>/</c> separators, and file URIs for paths outside it.
/// </summary>
internal static class CognitiveCiLocation
{
    /// <summary>The SARIF base identifier of paths relative to the working directory.</summary>
    internal const string SourceRootId = "%SRCROOT%";

    /// <summary>The line of the method, at least 1, because the issue formats cannot express a line before the first.</summary>
    internal static int Line(CognitiveMetrics metrics) => Math.Max(1, metrics.methodLineNumber);

    /// <summary>
    /// The file relative to the current working directory when it lies below it, otherwise its absolute path. Both
    /// use <c>/</c> separators. UNC paths keep their <c>//server/share</c> prefix.
    /// </summary>
    internal static string DisplayPath(string file) => DisplayPath(file, Directory.GetCurrentDirectory());

    internal static string DisplayPath(string file, string workingDirectory)
    {
        bool windows = IsWindowsRuntimeOrStyle(file) || IsWindowsStyle(workingDirectory);
        string root = Collapse(Slashes(workingDirectory, windows), windows);
        string slashed = Slashes(file, windows);
        string absolute = IsRooted(slashed) ? Collapse(slashed, windows) : Collapse(root + "/" + slashed, windows);

        string prefix = root.EndsWith('/') ? root : root + "/";
        return absolute.StartsWith(prefix, PathComparison) ? absolute[prefix.Length..] : absolute;
    }

    /// <summary>Whether a display path is absolute and therefore needs a file URI of its own.</summary>
    internal static bool IsAbsolute(string displayPath) => IsRooted(displayPath);

    /// <summary>
    /// The file URI of an absolute path, such as <c>file:///home/a/My%20File.cs</c>, <c>file:///C:/work/A.cs</c>
    /// or <c>file://server/share/A.cs</c> for UNC paths. The string is built here instead of with <see cref="Uri"/>,
    /// so Windows paths give the same result everywhere.
    /// </summary>
    internal static string FileUri(string absolutePath)
    {
        if (IsDriveRooted(absolutePath))
        {
            return "file:///" + absolutePath[..2] + EncodeUriPath(absolutePath[2..]);
        }

        if (absolutePath.StartsWith("//", StringComparison.Ordinal))
        {
            return "file://" + EncodeUriPath(absolutePath[2..]);
        }

        return "file://" + EncodeUriPath(absolutePath);
    }

    /// <summary>The working directory as a file URI with a trailing slash, the base of every relative SARIF URI.</summary>
    internal static string WorkingDirectoryUri(string workingDirectory)
    {
        bool windows = IsWindowsRuntimeOrStyle(workingDirectory);
        string uri = FileUri(Collapse(Slashes(workingDirectory, windows), windows));
        return uri.EndsWith('/') ? uri : uri + "/";
    }

    /// <summary>Percent-encodes every segment of a path, keeping the <c>/</c> separators.</summary>
    internal static string EncodeUriPath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Backslashes are separators on Windows and in Windows-style input (a drive letter or a UNC prefix). Elsewhere a
    /// backslash is a character of the file name.
    /// </summary>
    private static bool IsWindowsRuntimeOrStyle(string path) => OperatingSystem.IsWindows() || IsWindowsStyle(path);

    private static bool IsWindowsStyle(string path) =>
        path.StartsWith(@"\\", StringComparison.Ordinal) || IsDriveRootedRaw(path);

    private static string Slashes(string path, bool windows) => windows ? path.Replace('\\', '/') : path;

    private static bool IsRooted(string slashed) => slashed.StartsWith('/') || IsDriveRooted(slashed);

    private static bool IsDriveRooted(string slashed) =>
        slashed.Length >= 3 && char.IsAsciiLetter(slashed[0]) && slashed[1] == ':' && slashed[2] == '/';

    private static bool IsDriveRootedRaw(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && (path[2] == '\\' || path[2] == '/');

    /// <summary>
    /// Resolves <c>.</c> and <c>..</c> segments and repeated separators. The prefix is kept: a drive, the UNC
    /// <c>//</c> (only for Windows-style paths) or the root.
    /// </summary>
    private static string Collapse(string slashed, bool windows)
    {
        string prefix = IsDriveRooted(slashed)
            ? slashed[..2]
            : windows && slashed.StartsWith("//", StringComparison.Ordinal) ? "//" : "/";

        var segments = new List<string>();
        foreach (string segment in slashed[prefix.Length..].Split('/'))
        {
            switch (segment)
            {
                case "" or ".":
                    break;
                case "..":
                    if (segments.Count > 0)
                    {
                        segments.RemoveAt(segments.Count - 1);
                    }

                    break;
                default:
                    segments.Add(segment);
                    break;
            }
        }

        string body = string.Join('/', segments);
        return prefix is "/" or "//" ? prefix + body : prefix + "/" + body;
    }
}
