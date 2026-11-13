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
    /// use <c>/</c> separators, and Windows-style input is accepted on every platform.
    /// </summary>
    internal static string DisplayPath(string file) => DisplayPath(file, Directory.GetCurrentDirectory());

    internal static string DisplayPath(string file, string workingDirectory)
    {
        string root = Collapse(workingDirectory.Replace('\\', '/'));
        string slashed = file.Replace('\\', '/');
        string absolute = IsRooted(slashed) ? Collapse(slashed) : Collapse(root + "/" + slashed);

        string prefix = root.EndsWith('/') ? root : root + "/";
        return absolute.StartsWith(prefix, PathComparison) ? absolute[prefix.Length..] : absolute;
    }

    /// <summary>Whether a display path is absolute and therefore needs a file URI of its own.</summary>
    internal static bool IsAbsolute(string displayPath) => IsRooted(displayPath);

    /// <summary>
    /// The file URI of an absolute path, such as <c>file:///home/a/My%20File.cs</c> or <c>file:///C:/work/A.cs</c>.
    /// The string is converted here instead of with <see cref="Uri"/>, so Windows paths give the same result everywhere.
    /// </summary>
    internal static string FileUri(string absolutePath) =>
        IsDriveRooted(absolutePath)
            ? "file:///" + absolutePath[..2] + EncodeUriPath(absolutePath[2..])
            : "file://" + EncodeUriPath(absolutePath);

    /// <summary>The working directory as a file URI with a trailing slash, the base of every relative SARIF URI.</summary>
    internal static string WorkingDirectoryUri(string workingDirectory)
    {
        string uri = FileUri(Collapse(workingDirectory.Replace('\\', '/')));
        return uri.EndsWith('/') ? uri : uri + "/";
    }

    /// <summary>Percent-encodes every segment of a path, keeping the <c>/</c> separators.</summary>
    internal static string EncodeUriPath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static bool IsRooted(string slashed) => slashed.StartsWith('/') || IsDriveRooted(slashed);

    private static bool IsDriveRooted(string slashed) =>
        slashed.Length >= 3 && char.IsAsciiLetter(slashed[0]) && slashed[1] == ':' && slashed[2] == '/';

    /// <summary>Resolves <c>.</c> and <c>..</c> segments and repeated separators, keeping a drive or the root.</summary>
    private static string Collapse(string slashed)
    {
        string prefix = IsDriveRooted(slashed) ? slashed[..2] : string.Empty;
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

        return prefix + "/" + string.Join('/', segments);
    }
}
