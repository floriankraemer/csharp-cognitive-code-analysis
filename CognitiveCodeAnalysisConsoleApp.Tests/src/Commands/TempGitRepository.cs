/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.ComponentModel;
using System.Diagnostics;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Commands;

/// <summary>A throw-away git repository whose commits have fixed dates.</summary>
internal sealed class TempGitRepository : IDisposable
{
    private TempGitRepository(string directory) => Directory = directory;

    public string Directory { get; }

    public string PathOf(string file) => Path.Combine(Directory, file);

    public static bool GitAvailable()
    {
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo("git", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
            });
            process!.WaitForExit();
            return process.ExitCode == 0;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    public static TempGitRepository Create()
    {
        if (!GitAvailable())
        {
            Assert.Ignore("git is not installed.");
        }

        string directory = Path.Combine(Path.GetTempPath(), $"cca-churn-cli-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(directory);
        var repo = new TempGitRepository(directory);
        repo.Git("2020-01-01T12:00:00+0000", "init", "-q", "-b", "main");
        return repo;
    }

    public void WriteFile(string file, string content) => File.WriteAllText(PathOf(file), content);

    /// <summary>Appends a line to the file, so that every commit changes it, and commits it on the given UTC date.</summary>
    public void Commit(string file, string utcDateTime)
    {
        File.AppendAllText(PathOf(file), $"// {utcDateTime}\n");
        Git(utcDateTime, "add", "--", file);
        Git(utcDateTime, "commit", "-q", "-m", $"change {file}");
    }

    private void Git(string utcDateTime, params string[] arguments)
    {
        var info = new ProcessStartInfo("git")
        {
            WorkingDirectory = Directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (string argument in new[] { "-c", "user.name=Test", "-c", "user.email=test@example.com", "-c", "commit.gpgsign=false" }
            .Concat(arguments))
        {
            info.ArgumentList.Add(argument);
        }

        info.Environment["GIT_AUTHOR_DATE"] = utcDateTime;
        info.Environment["GIT_COMMITTER_DATE"] = utcDateTime;

        using Process process = Process.Start(info)!;
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.That(process.ExitCode, Is.Zero, $"git {string.Join(' ', arguments)}: {stderr.Result}");
    }

    public void Dispose()
    {
        foreach (string file in System.IO.Directory.EnumerateFiles(Directory, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        System.IO.Directory.Delete(Directory, recursive: true);
    }
}
