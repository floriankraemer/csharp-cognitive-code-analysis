/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;
using CognitiveCodeAnalysisConsoleApp.Tests.Reports;

using Spectre.Console;
using Spectre.Console.Testing;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Commands;

/// <summary>
/// The <c>init</c> command through the real composition root. Questions are answered on a Spectre test console, the
/// way a user answers them in a terminal.
/// </summary>
public class InitCommandTests
{
    private const string ThresholdError = "Score threshold must be a number greater than 0, for example 0.5.";

    private string _directory = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"cca-init-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _path = Path.Combine(_directory, ConfigurationResolver.DefaultFileName);
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

    private static TestConsole Terminal(params string[] answers)
    {
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        console.Profile.Width = 400;
        foreach (string answer in answers)
        {
            console.Input.PushTextWithEnter(answer);
        }

        return console;
    }

    private static int RunInit(TestConsole console, params string[] args) =>
        RunInitIn(console, new FixedConsoleEnvironment(OutputIsInteractive: true, InputIsRedirected: false), args);

    private static int RunInitIn(TestConsole console, IConsoleEnvironment environment, params string[] args)
    {
        IAnsiConsole previous = AnsiConsole.Console;
        AnsiConsole.Console = console;
        try
        {
            return Program.Run(["init", .. args], environment);
        }
        finally
        {
            AnsiConsole.Console = previous;
        }
    }

    private sealed record FixedConsoleEnvironment(bool OutputIsInteractive, bool InputIsRedirected) : IConsoleEnvironment;

    private static ConfigInitAnswers Load(string path) =>
        ConfigInitAnswers.FromConfiguration(ConfigurationLoader.Load(path));

    [Test]
    public void Silent_WritesTheDefaultsWithoutAskingAnyQuestion()
    {
        var console = Terminal();

        int exitCode = RunInit(console, "--silent", "-p", _path);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(console.Output, Does.Not.Contain("Score threshold"));
        Assert.That(console.Output, Does.Contain($"Created config at: {Path.GetFullPath(_path)}"));
        Assert.That(File.ReadAllText(_path), Is.EqualTo(ConfigInitializer.Render(Defaults)));
    }

    [Test]
    public void NonInteractiveConsole_WritesTheDefaultsWithoutAskingAnyQuestion()
    {
        using var capture = new ConsoleOutputCapture(interactive: false);

        int exitCode = Program.Main(["init", "-p", _path]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(capture.StdOut, Does.Not.Contain("Score threshold"));
        Assert.That(capture.StdOut, Does.Contain($"Created config at: {Path.GetFullPath(_path)}"));
        Assert.That(File.ReadAllText(_path), Is.EqualTo(ConfigInitializer.Render(Defaults)));
    }

    [Test]
    public void RedirectedStandardInput_WritesTheDefaultsWithoutAskingAnyQuestion()
    {
        var console = Terminal("1.5", "n", "n", "n", "n", "n", "n");

        int exitCode = RunInitIn(
            console,
            new FixedConsoleEnvironment(OutputIsInteractive: true, InputIsRedirected: true),
            "-p",
            _path);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(console.Output, Does.Not.Contain("Score threshold"));
        Assert.That(File.ReadAllText(_path), Is.EqualTo(ConfigInitializer.Render(Defaults)));
    }

    [Test]
    public void Silent_OutputIsByteIdenticalToGenerateConfig()
    {
        string generatedDirectory = Path.Combine(_directory, "generated");
        using (new ConsoleOutputCapture(interactive: false))
        {
            Assert.That(Program.Main(["--generate-config", generatedDirectory]), Is.EqualTo(0));
        }

        int exitCode = RunInit(Terminal(), "--silent", "-p", _path);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(File.ReadAllBytes(_path), Is.EqualTo(File.ReadAllBytes(Path.Combine(generatedDirectory, ConfigurationResolver.DefaultFileName))));
    }

    [Test]
    public void Interactive_EachQuestionIsPrecededByItsExplanation()
    {
        var console = Terminal("", "", "", "", "", "", "");

        RunInit(console, "-p", _path);

        string[] pairs =
        [
            "Methods with a score above this threshold are considered complex and are highlighted.",
            "Score threshold?",
            "Show only methods whose score exceeds the threshold, to focus on the most complex ones.",
            "Show only methods exceeding the threshold?",
            "Group the results into one table per class instead of a single flat table.",
            "Group results by class?",
        ];
        int previous = -1;
        foreach (string text in pairs)
        {
            int index = console.Output.IndexOf(text, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThan(previous), $"'{text}' is missing or out of order");
            previous = index;
        }
    }

    [Test]
    public void Interactive_AcceptingEveryDefault_WritesTheDefaults()
    {
        var console = Terminal("", "", "", "", "", "", "");

        int exitCode = RunInit(console, "-p", _path);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(console.Output, Does.Contain("Score threshold?"));
        Assert.That(console.Output, Does.Contain("Score threshold? (0.5):"));
        Assert.That(File.ReadAllText(_path), Is.EqualTo(ConfigInitializer.Render(Defaults)));
    }

    [Test]
    public void Interactive_CustomAnswers_AreWrittenIntoTheFile()
    {
        var console = Terminal("1.25", "n", "y", "y", "y", "n", "n");

        int exitCode = RunInit(console, "-p", _path);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Load(_path), Is.EqualTo(new ConfigInitAnswers(
            ScoreThreshold: 1.25,
            ShowOnlyMethodsExceedingThreshold: false,
            ShowHalsteadComplexity: true,
            ShowCyclomaticComplexity: true,
            ShowCouplingMetrics: true,
            ShowDetailedCognitiveMetrics: false,
            GroupByClass: false
        )));
    }

    [Test]
    public void Interactive_InvalidScoreThreshold_AsksAgainUntilItIsValid()
    {
        var console = Terminal("abc", "0", "-2", "0.25", "", "", "", "", "", "");

        int exitCode = RunInit(console, "-p", _path);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(CountOccurrences(console.Output, ThresholdError), Is.EqualTo(3));
        Assert.That(Load(_path).ScoreThreshold, Is.EqualTo(0.25));
    }

    [Test]
    public void Interactive_ValidThresholdOnTheFirstTry_IsNotReportedAsInvalid()
    {
        var console = Terminal("0.5", "", "", "", "", "", "");

        int exitCode = RunInit(console, "-p", _path);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(console.Output, Does.Not.Contain(ThresholdError));
        Assert.That(Load(_path).ScoreThreshold, Is.EqualTo(0.5));
    }

    [Test]
    public void Interactive_ExistingFileWithoutForce_RefusesBeforeAskingAnyQuestion()
    {
        File.WriteAllText(_path, "old");
        var console = Terminal("1.5");

        int exitCode = RunInit(console, "-p", _path);

        Assert.That(exitCode, Is.EqualTo(-1));
        Assert.That(console.Output, Does.Contain("Config file already exists at"));
        Assert.That(console.Output, Does.Contain("Use --force to overwrite."));
        Assert.That(console.Output, Does.Not.Contain("Score threshold"));
        Assert.That(File.ReadAllText(_path), Is.EqualTo("old"));
    }

    [Test]
    public void ExistingFileWithoutForce_IsNotOverwritten()
    {
        File.WriteAllText(_path, "old");

        int exitCode = RunInit(Terminal(), "--silent", "-p", _path);

        Assert.That(exitCode, Is.EqualTo(-1));
        Assert.That(File.ReadAllText(_path), Is.EqualTo("old"));
    }

    [Test]
    public void ExistingFileWithForce_IsReplacedWithTheDefaults()
    {
        File.WriteAllText(_path, "old");

        int exitCode = RunInit(Terminal(), "--silent", "--force", "-p", _path);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(File.ReadAllText(_path), Is.EqualTo(ConfigInitializer.Render(Defaults)));
    }

    [Test]
    public void NestedMissingDirectories_AreCreated()
    {
        string path = Path.Combine(_directory, "nested", "config", "settings.json");

        int exitCode = RunInit(Terminal(), "--silent", "-p", path);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public void DirectoryTarget_IsRefusedWithAClearError()
    {
        var console = Terminal();

        int exitCode = RunInit(console, "--silent", "--force", "-p", _directory);

        Assert.That(exitCode, Is.EqualTo(-1));
        Assert.That(console.Output, Does.Contain("it is a directory"));
    }

    [Test]
    public void EmptyPath_IsRefusedWithAClearError()
    {
        var console = Terminal();

        int exitCode = RunInit(console, "--silent", "-p", "");

        Assert.That(exitCode, Is.EqualTo(-1));
        Assert.That(console.Output, Does.Contain("path must not be empty"));
    }

    [Test]
    public void WithoutPath_WritesTheFileToTheCurrentDirectory()
    {
        string previous = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(_directory);
        try
        {
            int exitCode = RunInit(Terminal(), "--silent");

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(File.Exists(Path.Combine(_directory, ConfigurationResolver.DefaultFileName)), Is.True);
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }
    }

    [Test]
    public void WrittenFile_LoadsWithoutWarningsAndDrivesAnAnalysis()
    {
        string source = Path.Combine(_directory, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Sample.cs"), "public class Sample { public void M() { if (true) { } } }");
        RunInit(Terminal("1.25", "", "", "", "", "", ""), "-p", _path);

        using var capture = new ConsoleOutputCapture();
        int exitCode = Program.Main([source, "-c", _path]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(capture.Text + capture.StdOut, Does.Not.Contain("Warning"));
        Assert.That(capture.Text, Does.Contain("Summary:"));
    }

    [Test]
    public void AnalysisStillRunsForAPath_WhenInitIsRegistered()
    {
        string source = Path.Combine(_directory, "src");
        Directory.CreateDirectory(source);
        File.WriteAllText(Path.Combine(source, "Sample.cs"), "public class Sample { public void M() { } }");

        using var capture = new ConsoleOutputCapture();
        int exitCode = Program.Main([source]);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(capture.Text, Does.Contain("Summary:"));
    }

    private static int CountOccurrences(string text, string value)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
