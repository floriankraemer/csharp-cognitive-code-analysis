/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Churn;

namespace CognitiveCodeAnalysis.Tests.Churn;

public class GitChangeCounterTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private static GitChangeCounter Counter(string since) =>
        new("git", DateOnly.ParseExact(since, "yyyy-MM-dd"), Timeout);

    [Test]
    public void Count_CountsOnlyTheCommitsThatTouchedTheFile()
    {
        using var repo = TempGitRepo.Create();
        repo.Commit("A.cs", "2024-02-01T12:00:00+0000");
        repo.Commit("B.cs", "2024-02-02T12:00:00+0000");
        repo.Commit("A.cs", "2024-02-03T12:00:00+0000");

        Assert.That(Counter("2000-01-01").Count(repo.PathOf("A.cs")), Is.EqualTo(2));
        Assert.That(Counter("2000-01-01").Count(repo.PathOf("B.cs")), Is.EqualTo(1));
    }

    [Test]
    public void Count_IgnoresCommitsBeforeTheSinceDateAndCountsTheWholeSinceDay()
    {
        using var repo = TempGitRepo.Create();
        repo.Commit("A.cs", "2023-12-31T12:00:00+0000");
        repo.Commit("A.cs", "2024-01-01T12:00:00+0000");
        repo.Commit("A.cs", "2024-06-01T12:00:00+0000");

        Assert.That(Counter("2024-01-01").Count(repo.PathOf("A.cs")), Is.EqualTo(2), "noon of the since day counts");
        Assert.That(Counter("2024-01-02").Count(repo.PathOf("A.cs")), Is.EqualTo(1));
        Assert.That(Counter("2025-01-01").Count(repo.PathOf("A.cs")), Is.Zero);
    }

    [Test]
    public void Count_IgnoresMergeCommits()
    {
        using var repo = TempGitRepo.Create();
        repo.Commit("A.cs", "2024-02-01T12:00:00+0000");
        repo.GitAt("2024-02-02T12:00:00+0000", "checkout", "-q", "-b", "feature");
        repo.Commit("A.cs", "2024-02-02T12:00:00+0000");
        repo.GitAt("2024-02-03T12:00:00+0000", "checkout", "-q", "main");
        repo.Commit("B.cs", "2024-02-03T12:00:00+0000");
        repo.GitAt("2024-02-04T12:00:00+0000", "merge", "-q", "--no-ff", "-m", "merge feature", "feature");

        Assert.That(Counter("2000-01-01").Count(repo.PathOf("A.cs")), Is.EqualTo(2));
    }

    [Test]
    public void Count_OfAFileGitDoesNotTrackIsZero()
    {
        using var repo = TempGitRepo.Create();
        repo.Commit("A.cs", "2024-02-01T12:00:00+0000");
        File.WriteAllText(repo.PathOf("Untracked.cs"), "class U {}");

        Assert.That(Counter("2000-01-01").Count(repo.PathOf("Untracked.cs")), Is.Zero);
    }

    [Test]
    public void Count_WorksForFilesInSubdirectoriesAndPathsWithSpacesAndQuotes()
    {
        using var repo = TempGitRepo.Create();
        repo.Commit("sub dir/it's \"odd\" $HOME;name.cs", "2024-02-01T12:00:00+0000");

        Assert.That(Counter("2000-01-01").Count(repo.PathOf("sub dir/it's \"odd\" $HOME;name.cs")), Is.EqualTo(1));
    }

    [Test]
    public void Count_MatchesGlobCharactersInFileNamesLiterally()
    {
        using var repo = TempGitRepo.Create();
        repo.Commit("C1.cs", "2024-02-01T12:00:00+0000");
        repo.Commit("C[1].cs", "2024-02-02T12:00:00+0000");
        repo.Commit("C[1].cs", "2024-02-03T12:00:00+0000");
        repo.Commit("star.cs", "2024-02-04T12:00:00+0000");

        Assert.That(Counter("2000-01-01").Count(repo.PathOf("C1.cs")), Is.EqualTo(1));
        Assert.That(Counter("2000-01-01").Count(repo.PathOf("C[1].cs")), Is.EqualTo(2));
        Assert.That(Counter("2000-01-01").Count(repo.PathOf("*.cs")), Is.Zero);
    }

    [Test]
    public void Count_FileOutsideAnyRepositoryFailsWithNotAGitRepository()
    {
        var temp = new TempFiles();
        try
        {
            if (!TempGitRepo.GitAvailable())
            {
                Assert.Ignore("git is not installed.");
            }

            string file = temp.CreateFileWithContent("A.cs", "class A {}");

            // A temp directory is outside any repository unless GIT_DIR or a parent repository says otherwise.
            ChurnException? exception = Assert.Throws<ChurnException>(() => Counter("2000-01-01").Count(file));

            Assert.That(exception!.Message, Does.StartWith("Not a git repository: "));
        }
        finally
        {
            temp.CleanUp();
        }
    }

    [Test]
    public void Count_InARepositoryWithoutCommitsFailsWithGitsMessage()
    {
        using var repo = TempGitRepo.Create();
        File.WriteAllText(repo.PathOf("A.cs"), "class A {}");

        ChurnException? exception = Assert.Throws<ChurnException>(() => Counter("2000-01-01").Count(repo.PathOf("A.cs")));

        Assert.That(exception!.Message, Does.StartWith("git failed with exit code "));
    }

    [Test]
    public void Count_MissingGitExecutableIsReportedClearly()
    {
        var counter = new GitChangeCounter("definitely-not-a-git-executable", new DateOnly(2000, 1, 1), Timeout);

        ChurnException? exception = Assert.Throws<ChurnException>(() => counter.Count("/tmp/A.cs"));

        Assert.That(exception!.Message, Does.StartWith("git executable not found"));
        Assert.That(exception.InnerException, Is.Not.Null);
    }

    [Test]
    public void Count_PassesTheDocumentedArgumentsWithoutAShell()
    {
        string script = FakeGit.Script("printf '%s\\n' \"$@\" > \"$0.args\"; echo 7");
        try
        {
            int count = new GitChangeCounter(script, new DateOnly(2024, 3, 9), Timeout).Count("/some dir/A.cs");

            Assert.That(count, Is.EqualTo(7));
            Assert.That(File.ReadAllLines(script + ".args"), Is.EqualTo(new[]
            {
                "--literal-pathspecs", "-C", "/some dir", "rev-list", "--since=2024-03-09 00:00:00", "--no-merges", "--count", "HEAD", "--",
                "/some dir/A.cs",
            }));
        }
        finally
        {
            FakeGit.Delete(script);
        }
    }

    [Test]
    public void Count_RunsGitWithTheCLocaleSoMessagesAreStable()
    {
        string script = FakeGit.Script("echo \"$LC_ALL\" > \"$0.env\"; echo 1");
        try
        {
            new GitChangeCounter(script, new DateOnly(2024, 3, 9), Timeout).Count("/A.cs");

            Assert.That(File.ReadAllText(script + ".env").Trim(), Is.EqualTo("C"));
        }
        finally
        {
            FakeGit.Delete(script);
        }
    }

    [Test]
    public void Count_NonZeroExitIsReportedWithExitCodeAndStderr()
    {
        string script = FakeGit.Script("echo 'fatal: bad revision' >&2; exit 3");
        try
        {
            ChurnException? exception = Assert.Throws<ChurnException>(
                () => new GitChangeCounter(script, new DateOnly(2000, 1, 1), Timeout).Count("/A.cs"));

            Assert.That(exception!.Message, Is.EqualTo("git failed with exit code 3: fatal: bad revision"));
        }
        finally
        {
            FakeGit.Delete(script);
        }
    }

    [Test]
    public void Count_UnexpectedOutputIsAnError()
    {
        string script = FakeGit.Script("echo not-a-number");
        try
        {
            ChurnException? exception = Assert.Throws<ChurnException>(
                () => new GitChangeCounter(script, new DateOnly(2000, 1, 1), Timeout).Count("/A.cs"));

            Assert.That(exception!.Message, Does.Contain("Unexpected git output").And.Contain("not-a-number"));
        }
        finally
        {
            FakeGit.Delete(script);
        }
    }

    [Test]
    public void Count_ReadsOutputLargerThanAPipeBufferWithoutBlocking()
    {
        string script = FakeGit.Script("head -c 300000 /dev/zero | tr '\\0' x >&2; echo 4");
        try
        {
            Assert.That(new GitChangeCounter(script, new DateOnly(2000, 1, 1), Timeout).Count("/A.cs"), Is.EqualTo(4));
        }
        finally
        {
            FakeGit.Delete(script);
        }
    }

    [Test]
    public void Count_GitThatDoesNotFinishInTimeIsKilledAndReported()
    {
        string script = FakeGit.Script("sleep 30");
        try
        {
            var counter = new GitChangeCounter(script, new DateOnly(2000, 1, 1), TimeSpan.FromMilliseconds(300));

            ChurnException? exception = Assert.Throws<ChurnException>(() => counter.Count("/A.cs"));

            Assert.That(exception!.Message, Is.EqualTo("git did not finish within 300 ms"));
        }
        finally
        {
            FakeGit.Delete(script);
        }
    }

    /// <summary>Shell scripts that stand in for git; they cannot run on Windows.</summary>
    private static class FakeGit
    {
        public static string Script(string body)
        {
            string path = Path.Combine(Path.GetTempPath(), $"fake-git-{Guid.NewGuid():N}.sh");
            if (OperatingSystem.IsWindows())
            {
                Assert.Ignore("Fake git scripts need a POSIX shell.");
                return path;
            }

            File.WriteAllText(path, "#!/bin/sh\n" + body + "\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return path;
        }

        public static void Delete(string script)
        {
            foreach (string file in new[] { script, script + ".args", script + ".env" })
            {
                File.Delete(file);
            }
        }
    }
}
