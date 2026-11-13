/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.Configuration;

public class ExcludePatternConfigurationTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private CognitiveConfiguration Load(string cognitiveSection) =>
        ConfigurationLoader.Load(_temp.CreateFileWithContent("settings.json", $$"""{ "cognitive": {{{cognitiveSection}}} }"""));

    [Test]
    public void ValidPatterns_AreLoadedWithoutWarnings()
    {
        var configuration = Load("""
            "excludeFilePatterns": ["/Generated/", "Tests\\.cs$"],
            "excludePatterns": ["::Get"]
            """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(configuration.ExcludeFilePatterns, Is.EqualTo(new[] { "/Generated/", @"Tests\.cs$" }));
            Assert.That(configuration.ExcludePatterns, Is.EqualTo(new[] { "::Get" }));
            Assert.That(configuration.Warnings, Is.Empty);
        }
    }

    [Test]
    public void InvalidPatterns_AreDroppedWithAWarningNamingPatternAndKey()
    {
        var configuration = Load("""
            "excludeFilePatterns": ["(", "/Generated/"],
            "excludePatterns": ["::Get", "[a-"]
            """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(configuration.ExcludeFilePatterns, Is.EqualTo(new[] { "/Generated/" }));
            Assert.That(configuration.ExcludePatterns, Is.EqualTo(new[] { "::Get" }));
            Assert.That(configuration.Warnings, Has.Count.EqualTo(2));
            Assert.That(configuration.Warnings[0], Does.StartWith("Invalid regular expression '(' in 'excludeFilePatterns' is ignored: "));
            Assert.That(configuration.Warnings[1], Does.StartWith("Invalid regular expression '[a-' in 'excludePatterns' is ignored: "));
        }
    }

    [Test]
    public void MissingKeys_DefaultToNoPatterns()
    {
        var configuration = Load("\"scoreThreshold\": 1");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(configuration.ExcludeFilePatterns, Is.Empty);
            Assert.That(configuration.ExcludePatterns, Is.Empty);
        }
    }
}
