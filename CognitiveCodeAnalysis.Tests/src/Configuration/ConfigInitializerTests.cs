/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.Json.Nodes;

using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.Configuration;

public class ConfigInitializerTests
{
    private static readonly ConfigInitAnswers Custom = new(
        ScoreThreshold: 1.25,
        ShowOnlyMethodsExceedingThreshold: false,
        ShowHalsteadComplexity: true,
        ShowCyclomaticComplexity: true,
        ShowCouplingMetrics: true,
        ShowDetailedCognitiveMetrics: false,
        GroupByClass: false
    );

    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"cca-init-lib-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static ConfigInitAnswers Defaults => ConfigInitAnswers.FromConfiguration(CognitiveConfigurationDefaults.Create());

    private string Target(params string[] parts) => Path.Combine([_directory, .. parts]);

    [Test]
    public void FromConfiguration_ReadsTheSevenAnsweredSettingsFromTheShippedDefaults()
    {
        Assert.That(Defaults, Is.EqualTo(new ConfigInitAnswers(
            ScoreThreshold: 0.5,
            ShowOnlyMethodsExceedingThreshold: true,
            ShowHalsteadComplexity: false,
            ShowCyclomaticComplexity: false,
            ShowCouplingMetrics: false,
            ShowDetailedCognitiveMetrics: true,
            GroupByClass: true
        )));
    }

    [Test]
    public void Render_WithDefaultAnswers_MatchesTheGoldenFile()
    {
        string golden = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "init", "defaults.json"));

        Assert.That(ConfigInitializer.Render(Defaults), Is.EqualTo(golden));
    }

    [Test]
    public void Render_WithDefaultAnswers_KeepsEverySettingOfTheShippedFile()
    {
        JsonNode shipped = JsonNode.Parse(CognitiveConfigurationDefaults.SettingsJson)!;
        JsonNode rendered = JsonNode.Parse(ConfigInitializer.Render(Defaults))!;

        Assert.That(JsonNode.DeepEquals(rendered, shipped), Is.True);
    }

    [Test]
    public void Render_WithCustomAnswers_AppliesEachAnswer()
    {
        JsonObject cognitive = JsonNode.Parse(ConfigInitializer.Render(Custom))!["cognitive"]!.AsObject();

        Assert.That((double)cognitive["scoreThreshold"]!, Is.EqualTo(1.25));
        Assert.That((bool)cognitive["showOnlyMethodsExceedingThreshold"]!, Is.False);
        Assert.That((bool)cognitive["showHalsteadComplexity"]!, Is.True);
        Assert.That((bool)cognitive["showCyclomaticComplexity"]!, Is.True);
        Assert.That((bool)cognitive["showCouplingMetrics"]!, Is.True);
        Assert.That((bool)cognitive["showDetailedCognitiveMetrics"]!, Is.False);
        Assert.That((bool)cognitive["groupByClass"]!, Is.False);
    }

    [Test]
    public void Render_WithCustomAnswers_KeepsTheMetricsAndTheKeyOrderOfTheShippedFile()
    {
        JsonObject shipped = JsonNode.Parse(CognitiveConfigurationDefaults.SettingsJson)!["cognitive"]!.AsObject();
        JsonObject rendered = JsonNode.Parse(ConfigInitializer.Render(Custom))!["cognitive"]!.AsObject();

        Assert.That(rendered["metrics"]!.ToJsonString(), Is.EqualTo(shipped["metrics"]!.ToJsonString()));
        Assert.That(rendered.Select(property => property.Key), Is.EqualTo(shipped.Select(property => property.Key)));
    }

    [Test]
    public void Render_EndsWithANewlineAndUsesNoByteOrderMark()
    {
        string rendered = ConfigInitializer.Render(Defaults);

        Assert.That(rendered, Does.EndWith("}\n"));
        Assert.That(rendered, Does.Not.Contain("\r"));
    }

    [Test]
    public void Write_WithNewFile_CreatesTheFileWithTheContent()
    {
        string path = Target("settings.json");

        string fullPath = ConfigInitializer.PrepareTarget(path, overwrite: false);

        ConfigInitializer.Write(fullPath, "{}\n", overwrite: false);

        Assert.That(fullPath, Is.EqualTo(Path.GetFullPath(path)));
        Assert.That(File.ReadAllText(path), Is.EqualTo("{}\n"));
    }

    [Test]
    public void Write_WithMissingParentDirectories_CreatesThem()
    {
        string path = Target("nested", "config", "settings.json");

        ConfigInitializer.Write(ConfigInitializer.PrepareTarget(path, overwrite: false), "{}\n", overwrite: false);

        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public void Write_WithoutOverwriteAndExistingFile_ThrowsAndKeepsTheFile()
    {
        string path = Target("settings.json");
        File.WriteAllText(path, "old");

        var exception = Assert.Throws<ConfigFileException>(() =>
            ConfigInitializer.Write(Path.GetFullPath(path), "new", overwrite: false));

        Assert.That(exception!.Message, Does.StartWith("Config file already exists at "));
        Assert.That(exception.Message, Does.EndWith("Use --force to overwrite."));
        Assert.That(File.ReadAllText(path), Is.EqualTo("old"));
    }

    [Test]
    public void PrepareTarget_WithoutOverwriteAndExistingFile_ThrowsBeforeAnythingIsWritten()
    {
        string path = Target("settings.json");
        File.WriteAllText(path, "old");

        Assert.Throws<ConfigFileException>(() => ConfigInitializer.PrepareTarget(path, overwrite: false));
        Assert.That(File.ReadAllText(path), Is.EqualTo("old"));
    }

    [Test]
    public void PrepareTarget_WithOverwriteAndExistingFile_ReturnsThePath()
    {
        string path = Target("settings.json");
        File.WriteAllText(path, "old");

        Assert.That(ConfigInitializer.PrepareTarget(path, overwrite: true), Is.EqualTo(Path.GetFullPath(path)));
    }

    [Test]
    public void Write_WithOverwrite_ReplacesTheFileAndLeavesNoTemporaryFile()
    {
        string path = Target("settings.json");
        File.WriteAllText(path, "old");

        ConfigInitializer.Write(Path.GetFullPath(path), "new\n", overwrite: true);

        Assert.That(File.ReadAllText(path), Is.EqualTo("new\n"));
        Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1));
    }

    [Test]
    public void Write_WhenTheParentIsAFile_ThrowsAClearError()
    {
        string blocker = Target("blocker");
        File.WriteAllText(blocker, "not a directory");
        string path = Path.Combine(blocker, "settings.json");

        var exception = Assert.Throws<ConfigFileException>(() =>
            ConfigInitializer.Write(Path.GetFullPath(path), "{}", overwrite: false));

        Assert.That(exception!.Message, Does.StartWith($"Cannot write config file {Path.GetFullPath(path)}: "));
        Assert.That(exception.InnerException, Is.Not.Null);
    }

    [TestCase("")]
    [TestCase("   ")]
    public void PrepareTarget_WithEmptyPath_ThrowsAClearError(string path)
    {
        var exception = Assert.Throws<ConfigFileException>(() => ConfigInitializer.PrepareTarget(path, overwrite: false));

        Assert.That(exception!.Message, Is.EqualTo("Invalid path: path must not be empty."));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PrepareTarget_WhenTheTargetIsADirectory_ThrowsEvenWithForce(bool overwrite)
    {
        var exception = Assert.Throws<ConfigFileException>(() => ConfigInitializer.PrepareTarget(_directory, overwrite));

        Assert.That(exception!.Message, Is.EqualTo($"Cannot write config file {_directory}: it is a directory."));
    }

    [Test]
    public void PrepareTarget_WithInvalidPathCharacters_ThrowsAClearError()
    {
        string path = "bad\0name.json";

        var exception = Assert.Throws<ConfigFileException>(() => ConfigInitializer.PrepareTarget(path, overwrite: false));

        Assert.That(exception!.Message, Does.StartWith($"Invalid path {path}: "));
    }

    [Test]
    public void Render_WrittenAndLoaded_ReturnsTheAnswers()
    {
        string path = Target("settings.json");
        ConfigInitializer.Write(
            ConfigInitializer.PrepareTarget(path, overwrite: false),
            ConfigInitializer.Render(Custom),
            overwrite: false);

        CognitiveConfiguration loaded = ConfigurationLoader.Load(path);

        Assert.That(ConfigInitAnswers.FromConfiguration(loaded), Is.EqualTo(Custom));
        Assert.That(loaded.Warnings, Is.Empty);
    }
}
