/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysisConsoleApp.Commands;
using CognitiveCodeAnalysisConsoleApp.Application;
using CognitiveCodeAnalysisConsoleApp.Tests.Reports;

using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Application;

/// <summary>Drives <c>--cache</c> and <c>--no-cache</c> through the real composition root.</summary>
public class CacheEndToEndTests
{
    private string _dir = null!;
    private string _originalDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _originalDirectory = Directory.GetCurrentDirectory();
        _dir = Path.Combine(Path.GetTempPath(), $"cca-cache-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(_dir, "src"));
        File.WriteAllText(
            Path.Combine(_dir, "src", "Sample.cs"),
            "namespace Samples; public class Svc { public int Run(int a) { if (a > 0) { return 1; } return 0; } }");
        File.WriteAllText(P("on.json"), """{ "cognitive": { "showOnlyMethodsExceedingThreshold": false, "cache": { "enabled": true } } }""");
        File.WriteAllText(P("off.json"), """{ "cognitive": { "showOnlyMethodsExceedingThreshold": false } }""");
        Directory.SetCurrentDirectory(_dir);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.SetCurrentDirectory(_originalDirectory);
        Directory.Delete(_dir, recursive: true);
    }

    private string P(string name) => Path.Combine(_dir, name);

    private string CacheDirectory => Path.Combine(_dir, ".cca", "cache");

    private string Run(params string[] args)
    {
        using var output = new ConsoleOutputCapture();
        int exitCode = Program.Main(args);
        Assert.That(exitCode, Is.EqualTo(0), output.Text + output.StdOut);
        return output.Text;
    }

    private string Json(string config, params string[] extra)
    {
        string report = P("report.json");
        Run(["src", "-c", P(config), "-f", "Json", "-o", report, .. extra]);
        // The generation time differs on every run.
        return System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(report), "\"generatedAt\": \"[^\"]*\"", "\"generatedAt\": \"\"");
    }

    [Test]
    public void ByDefault_NoCacheDirectoryIsCreated()
    {
        Json("off.json");

        Assert.That(Directory.Exists(CacheDirectory), Is.False);
    }

    [Test]
    public void CacheFlag_CreatesTheCache_AndTwoRunsProduceTheSameOutput()
    {
        string first = Json("off.json", "--cache");
        string second = Json("off.json", "--cache");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.GetFiles(CacheDirectory, "*.json", SearchOption.AllDirectories), Has.Length.EqualTo(1));
            Assert.That(second, Is.EqualTo(first));
            Assert.That(first, Is.EqualTo(Json("off.json")));
        }
    }

    [Test]
    public void ConfigEnablesTheCache()
    {
        Json("on.json");

        Assert.That(Directory.Exists(CacheDirectory), Is.True);
    }

    [Test]
    public void NoCacheFlag_OverridesTheConfig()
    {
        Json("on.json", "--no-cache");

        Assert.That(Directory.Exists(CacheDirectory), Is.False);
    }

    [Test]
    public void ConsoleReport_UsesTheCacheToo()
    {
        string first = Run("src", "-c", P("off.json"), "--cache");
        string second = Run("src", "-c", P("off.json"), "--cache");

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void CacheAndNoCacheTogether_AreRejected()
    {
        var settings = new AnalyseCommandSettings { GenerateConfig = new FlagValue<string?>(), Cache = true, NoCache = true };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(settings.Validate().Successful, Is.False);
            Assert.That(settings.Validate().Message, Does.Contain("--cache").And.Contain("--no-cache"));
        }
    }

    [TestCase(false, false, null)]
    [TestCase(true, false, true)]
    [TestCase(false, true, false)]
    public void Mapper_TurnsTheFlagsIntoAnOverride(bool cache, bool noCache, bool? expected)
    {
        var settings = new AnalyseCommandSettings { GenerateConfig = new FlagValue<string?>(), Cache = cache, NoCache = noCache };

        Assert.That(AnalyseRequestMapper.FromSettings(settings).Cache, Is.EqualTo(expected));
        Assert.That(settings.Validate().Successful, Is.True);
    }
}
