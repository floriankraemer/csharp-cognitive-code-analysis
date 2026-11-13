/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace CognitiveCodeAnalysis.Churn;

/// <summary>
/// Counts the non-merge commits since the start of a day that touched a file, with one
/// <c>git rev-list --count</c> call per file. No shell is involved.
/// </summary>
public sealed class GitChangeCounter(string executable, DateOnly since, TimeSpan timeout) : IChangeCounter
{
    public const string DefaultExecutable = "git";

    public int Count(string file)
    {
        string fullPath = Path.GetFullPath(file);
        string directory = Path.GetDirectoryName(fullPath)!;
        string output = RevListCount(fullPath, directory);

        if (!int.TryParse(output, NumberStyles.None, CultureInfo.InvariantCulture, out int count))
        {
            throw new ChurnException($"Unexpected git output for {fullPath}: '{output}'");
        }

        return count;
    }

    private string RevListCount(string file, string directory)
    {
        using Process process = Start(file, directory);
        // Both streams are drained concurrently so that neither pipe can fill up and block git.
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            throw new ChurnException($"git did not finish within {(int)timeout.TotalMilliseconds} ms");
        }

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw Failure(process.ExitCode, stderr.GetAwaiter().GetResult().Trim(), directory);
        }

        return stdout.GetAwaiter().GetResult().Trim();
    }

    private Process Start(string file, string directory)
    {
        var info = new ProcessStartInfo(executable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        info.ArgumentList.Add("-C");
        info.ArgumentList.Add(directory);
        info.ArgumentList.Add("rev-list");
        info.ArgumentList.Add($"--since={since.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} 00:00:00");
        info.ArgumentList.Add("--no-merges");
        info.ArgumentList.Add("--count");
        info.ArgumentList.Add("HEAD");
        info.ArgumentList.Add("--");
        info.ArgumentList.Add(file);
        // Stable messages, so that "not a git repository" is recognisable.
        info.Environment["LC_ALL"] = "C";

        try
        {
            Process process = Process.Start(info)!;
            process.StandardInput.Close();
            return process;
        }
        catch (Win32Exception exception)
        {
            throw new ChurnException($"git executable not found: {executable}", exception);
        }
    }

    private static ChurnException Failure(int exitCode, string message, string directory) =>
        message.Contains("not a git repository", StringComparison.OrdinalIgnoreCase)
            ? new ChurnException($"Not a git repository: {directory}")
            : new ChurnException($"git failed with exit code {exitCode}: {message}");
}
