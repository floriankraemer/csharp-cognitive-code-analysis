/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Diagnostics;
using System.Runtime.InteropServices;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Commands;

[TestFixture]
public class PublishedBinaryCliTests
{
    private static string _publishDirectory = null!;
    private static string _executablePath = null!;
    private string _workingDirectory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _publishDirectory = Path.Combine(Path.GetTempPath(), $"cca-published-{Guid.NewGuid():N}");
        PublishConsoleApp(_publishDirectory);

        string executableName = OperatingSystem.IsWindows()
            ? "CognitiveCodeAnalysisConsoleApp.exe"
            : "CognitiveCodeAnalysisConsoleApp";
        _executablePath = Path.Combine(_publishDirectory, executableName);

        Assert.That(File.Exists(_executablePath), Is.True, $"Expected published executable at {_executablePath}");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        if (Directory.Exists(_publishDirectory))
        {
            Directory.Delete(_publishDirectory, recursive: true);
        }
    }

    [SetUp]
    public void SetUp()
    {
        _workingDirectory = Path.Combine(Path.GetTempPath(), $"cca-binary-run-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workingDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    [Test]
    public void Help_IncludesGenerateConfigOption()
    {
        (int exitCode, string output) = RunExecutable(_workingDirectory, "--help");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Contain("--generate-config"));
        Assert.That(output, Does.Contain("[PATH]"));
        Assert.That(output, Does.Not.Contain("Could not find color or style"));
    }

    [Test]
    public void GenerateConfig_WithoutPath_WritesFileToWorkingDirectory()
    {
        (int exitCode, _) = RunExecutable(_workingDirectory, "--generate-config");

        string expectedPath = Path.Combine(_workingDirectory, ConfigurationResolver.DefaultFileName);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(File.Exists(expectedPath), Is.True);
    }

    [Test]
    public void GenerateConfig_WithPath_WritesFileToTargetDirectory()
    {
        var targetDirectory = Path.Combine(_workingDirectory, "config-output");

        (int exitCode, _) = RunExecutable(_workingDirectory, "--generate-config", targetDirectory);

        string expectedPath = Path.Combine(targetDirectory, ConfigurationResolver.DefaultFileName);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(File.Exists(expectedPath), Is.True);
    }

    [Test]
    public void Analyze_WithoutConfig_PrintsDefaultConfigSource()
    {
        File.WriteAllText(
            Path.Combine(_workingDirectory, "Sample.cs"),
            """
            namespace Samples;

            public class Sample
            {
                public void M() { }
            }
            """
        );

        (int exitCode, string output) = RunExecutable(_workingDirectory, _workingDirectory, "-f", "ConsoleText");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Contain("Config: Default"));
    }

    private (int ExitCode, string Output) RunExecutable(string workingDirectory, params string[] args)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        foreach (string arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(TimeSpan.FromMinutes(2));

        return (process.ExitCode, stdout + stderr);
    }

    private static void PublishConsoleApp(string outputDirectory)
    {
        string solutionRoot = FindSolutionRoot();
        string consoleProject = Path.Combine(
            solutionRoot,
            "CognitiveCodeAnalysisConsoleApp",
            "CognitiveCodeAnalysisConsoleApp.csproj"
        );

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = solutionRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            },
        };

        process.StartInfo.ArgumentList.Add("publish");
        process.StartInfo.ArgumentList.Add(consoleProject);
        process.StartInfo.ArgumentList.Add("-c");
        process.StartInfo.ArgumentList.Add("Release");
        process.StartInfo.ArgumentList.Add("-r");
        process.StartInfo.ArgumentList.Add(RuntimeInformation.RuntimeIdentifier);
        process.StartInfo.ArgumentList.Add("--self-contained");
        process.StartInfo.ArgumentList.Add("true");
        process.StartInfo.ArgumentList.Add("-o");
        process.StartInfo.ArgumentList.Add(outputDirectory);

        process.Start();
        string stdout = process.StandardOutput.ReadToEnd();
        string stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(TimeSpan.FromMinutes(5));

        Assert.That(
            process.ExitCode,
            Is.EqualTo(0),
            $"dotnet publish failed.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}"
        );
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CognitiveCodeAnalysis.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate CognitiveCodeAnalysis.sln from test output directory.");
    }
}
