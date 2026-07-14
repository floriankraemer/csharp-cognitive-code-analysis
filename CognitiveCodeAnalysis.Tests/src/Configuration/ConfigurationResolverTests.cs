/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.Configuration;

public class ConfigurationResolverTests
{
    private string _tempDirectory = null!;
    private string _originalWorkingDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _originalWorkingDirectory = Directory.GetCurrentDirectory();
        _tempDirectory = Path.Combine(Path.GetTempPath(), "cogcfg-resolver-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDirectory);
        Directory.SetCurrentDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_originalWorkingDirectory);

        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // ignore
            }
        }
    }

    [Test]
    public void Resolve_ExplicitPath_ReturnsThatPath()
    {
        var explicitFile = Path.Combine(_tempDirectory, "custom.json");
        File.WriteAllText(explicitFile, """{ "cognitive": { "scoreThreshold": 1.0 } }""");

        var source = ConfigurationResolver.Resolve(explicitFile);

        Assert.That(source.IsDefault, Is.False);
        Assert.That(source.Path, Is.EqualTo(Path.GetFullPath(explicitFile)));
        Assert.That(source.Display, Is.EqualTo(Path.GetFullPath(explicitFile)));
    }

    [Test]
    public void Resolve_NoExplicitPath_CwdFileExists_ReturnsCwdPath()
    {
        var cwdFile = Path.Combine(_tempDirectory, ConfigurationResolver.DefaultFileName);
        File.WriteAllText(cwdFile, """{ "cognitive": { "scoreThreshold": 2.0 } }""");

        var source = ConfigurationResolver.Resolve(null);

        Assert.That(source.IsDefault, Is.False);
        Assert.That(source.Path, Is.EqualTo(Path.GetFullPath(cwdFile)));
    }

    [Test]
    public void Resolve_NoExplicitPath_NoCwdFile_ReturnsDefault()
    {
        var source = ConfigurationResolver.Resolve(null);

        Assert.That(source.IsDefault, Is.True);
        Assert.That(source.Path, Is.Null);
        Assert.That(source.Display, Is.EqualTo("Default"));
    }

    [Test]
    public void Resolve_ExplicitPath_TakesPriorityOverCwdFile()
    {
        var cwdFile = Path.Combine(_tempDirectory, ConfigurationResolver.DefaultFileName);
        File.WriteAllText(cwdFile, """{ "cognitive": { "scoreThreshold": 2.0 } }""");

        var explicitFile = Path.Combine(_tempDirectory, "override.json");
        File.WriteAllText(explicitFile, """{ "cognitive": { "scoreThreshold": 3.0 } }""");

        var source = ConfigurationResolver.Resolve(explicitFile);

        Assert.That(source.IsDefault, Is.False);
        Assert.That(source.Path, Is.EqualTo(Path.GetFullPath(explicitFile)));
    }
}
