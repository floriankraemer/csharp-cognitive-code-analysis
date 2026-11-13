/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Tests.Reporting;
using CognitiveCodeAnalysisConsoleApp.Tests.Reports;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Commands;

/// <summary>Custom reporters selected with <c>-r</c>, driven through <see cref="Program.Main"/> like a user would.</summary>
public class CustomReporterEndToEndTests
{
    private TempGitRepository _repo = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = TempGitRepository.Create();
        _repo.WriteFile("Sample.cs", "namespace Samples; public class Sample { public int Run(int a) { if (a > 0) { return 1; } return 0; } }");
        _repo.Commit("Sample.cs", "2024-02-01T12:00:00+0000");
        _repo.Commit("Sample.cs", "2024-03-01T12:00:00+0000");

        string plugins = Path.Combine(_repo.Directory, "config", "plugins");
        PluginBuilder.Compile(plugins, "Cognitive", PluginBuilder.CognitiveReporter);
        PluginBuilder.Compile(plugins, "Churn", PluginBuilder.ChurnReporter);
        Config("""
            "cognitive": { "hotspots": { "type": "Acme.ListingReporter", "assembly": "plugins/Cognitive.dll" } },
            "churn": { "classes": { "type": "Acme.ClassListing", "assembly": "plugins/Churn.dll" } }
            """);
    }

    [TearDown]
    public void TearDown() => _repo.Dispose();

    private string ConfigFile => Path.Combine(_repo.Directory, "config", "settings.json");

    private string Out => Path.Combine(_repo.Directory, "out", "report.txt");

    private void Config(string customReporters) =>
        File.WriteAllText(ConfigFile, "{ \"cognitive\": { \"scoreThreshold\": 0.5, \"customReporters\": { " + customReporters + " } } }");

    private (int ExitCode, string Output) Run(params string[] args)
    {
        using var capture = new ConsoleOutputCapture();
        int exitCode = Program.Main(args);
        return (exitCode, capture.Text + capture.StdOut + capture.StdErr);
    }

    [Test]
    public void Analyse_CustomReporterWritesTheFile()
    {
        var (exitCode, output) = Run(_repo.Directory, "-c", ConfigFile, "-r", "hotspots", "-o", Out);

        Assert.That(exitCode, Is.EqualTo(0), output);
        Assert.That(output, Does.Contain("hotspots report generated:"));
        Assert.That(File.ReadAllText(Out), Does.StartWith("threshold=0.5;baseline=False\n").And.Contain("Sample.Run"));
    }

    [Test]
    public void Analyse_UnknownTypeListsTheCustomKeys()
    {
        var (exitCode, output) = Run(_repo.Directory, "-c", ConfigFile, "-r", "pdf", "-o", Out);

        Assert.That(exitCode, Is.Not.Zero);
        Assert.That(output, Does.Contain("Unknown report type 'pdf'").And.Contain("Custom report types: hotspots."));
    }

    [Test]
    public void Analyse_CollidingKeyIsAnErrorOnlyWhenItsNameIsRequested()
    {
        Config("\"cognitive\": { \"JSON\": { \"type\": \"Acme.ListingReporter\", \"assembly\": \"plugins/Cognitive.dll\" } }");

        var (collision, collisionOutput) = Run(_repo.Directory, "-c", ConfigFile, "-r", "json", "-o", Out);
        var (other, otherOutput) = Run(_repo.Directory, "-c", ConfigFile, "-r", "csv", "-o", Out);

        Assert.That(collision, Is.Not.Zero);
        Assert.That(collisionOutput, Does.Contain("collides with the built-in report type 'Json'"));
        Assert.That(other, Is.EqualTo(0), otherOutput);
    }

    [Test]
    public void Analyse_FailingReporterExitsNonZeroWithTheCauseAndWritesNothing()
    {
        Config("\"cognitive\": { \"broken\": { \"type\": \"Acme.Missing\", \"assembly\": \"plugins/Cognitive.dll\" } }");

        var (exitCode, output) = Run(_repo.Directory, "-c", ConfigFile, "-r", "broken", "-o", Out);

        Assert.That(exitCode, Is.Not.Zero);
        Assert.That(output, Does.Contain("type Acme.Missing not found"));
        Assert.That(File.Exists(Out), Is.False);
    }

    [Test]
    public void Churn_CustomReporterWritesTheFile()
    {
        var (exitCode, output) = Run("churn", _repo.Directory, "-c", ConfigFile, "-s", "2024-01-01", "-r", "classes", "-o", Out);

        Assert.That(exitCode, Is.EqualTo(0), output);
        Assert.That(File.ReadAllText(Out), Does.StartWith("since=2024-01-01;coverage=False\n").And.Contain("Samples.Sample|"));
    }

    [Test]
    public void Churn_UnknownTypeListsTheBuiltInsAndCustomKeys()
    {
        var (exitCode, output) = Run("churn", _repo.Directory, "-c", ConfigFile, "-r", "pdf", "-o", Out);

        Assert.That(exitCode, Is.Not.Zero);
        Assert.That(output, Does.Contain("Invalid report type 'pdf'. Expected one of: Json, Csv, Html, Markdown, SvgTreemap. Custom report types: classes."));
        Assert.That(output, Does.Not.Contain("Samples.Sample"));
    }

    [TestCase("json")]
    [TestCase("SVG")]
    public void Churn_CollidingKeyIsAnErrorWhenItsNameOrAliasIsRequested(string key)
    {
        Config($"\"churn\": {{ \"{key}\": {{ \"type\": \"Acme.ClassListing\", \"assembly\": \"plugins/Churn.dll\" }} }}");

        var (collision, collisionOutput) = Run("churn", _repo.Directory, "-c", ConfigFile, "-r", key, "-o", Out);
        var (other, otherOutput) = Run("churn", _repo.Directory, "-c", ConfigFile, "-r", "csv", "-o", Out);

        Assert.That(collision, Is.Not.Zero);
        Assert.That(collisionOutput, Does.Contain("collides with the built-in report type"));
        Assert.That(other, Is.EqualTo(0), otherOutput);
    }
}
