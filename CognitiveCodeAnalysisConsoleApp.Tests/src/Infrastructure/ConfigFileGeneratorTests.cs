/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Infrastructure;

public class ConfigFileGeneratorTests
{
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"cca-config-gen-{Guid.NewGuid():N}");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Test]
    public void Generate_WithExplicitDirectory_WritesFileToThatDirectory()
    {
        var writtenPath = ConfigFileGenerator.Generate(_tempDirectory);

        Assert.That(writtenPath, Is.EqualTo(Path.Combine(_tempDirectory, ConfigurationResolver.DefaultFileName)));
        Assert.That(File.Exists(writtenPath), Is.True);
    }

    [Test]
    public void Generate_WritesValidJson_WithCognitiveSection()
    {
        var writtenPath = ConfigFileGenerator.Generate(_tempDirectory);
        var content = File.ReadAllText(writtenPath);

        Assert.That(content, Does.Contain("\"cognitive\""));
        Assert.That(content, Does.Contain("\"scoreThreshold\""));
        Assert.That(content, Does.Contain("\"metrics\""));
    }

    [Test]
    public void Generate_CreatesDirectory_WhenTargetDoesNotExist()
    {
        var nestedDirectory = Path.Combine(_tempDirectory, "nested", "config");

        var writtenPath = ConfigFileGenerator.Generate(nestedDirectory);

        Assert.That(Directory.Exists(nestedDirectory), Is.True);
        Assert.That(File.Exists(writtenPath), Is.True);
    }
}
