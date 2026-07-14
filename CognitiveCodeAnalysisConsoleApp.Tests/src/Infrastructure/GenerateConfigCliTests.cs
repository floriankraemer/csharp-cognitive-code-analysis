/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysisConsoleApp;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Infrastructure;

public class GenerateConfigCliTests
{
    private string _tempDirectory = null!;
    private string _originalWorkingDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _originalWorkingDirectory = Directory.GetCurrentDirectory();
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"cca-generate-cli-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        Directory.SetCurrentDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_originalWorkingDirectory);

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Main_WithGenerateConfigNoPath_WritesToCwdAndExitsZero()
    {
        int exitCode = Program.Main(["--generate-config"]);

        var expectedPath = Path.Combine(_tempDirectory, ConfigurationResolver.DefaultFileName);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(File.Exists(expectedPath), Is.True);
    }

    [Test]
    public void Main_WithGenerateConfigAndPath_WritesToGivenDirectory()
    {
        var targetDirectory = Path.Combine(_tempDirectory, "output");

        int exitCode = Program.Main(["--generate-config", targetDirectory]);

        var expectedPath = Path.Combine(targetDirectory, ConfigurationResolver.DefaultFileName);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(File.Exists(expectedPath), Is.True);
    }

    [Test]
    public void Main_WithGenerateConfig_SkipsAnalysis()
    {
        var sourceDirectory = Path.Combine(_tempDirectory, "src");
        Directory.CreateDirectory(sourceDirectory);
        File.WriteAllText(Path.Combine(sourceDirectory, "Sample.cs"), "class Sample { void M() { } }");

        int exitCode = Program.Main(["--generate-config", _tempDirectory, sourceDirectory, "-o", "report.html"]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(File.Exists(Path.Combine(_tempDirectory, ConfigurationResolver.DefaultFileName)), Is.True);
        Assert.That(File.Exists(Path.Combine(_tempDirectory, "report.html")), Is.False);
    }
}
