/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Churn;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.Reporting;

namespace CognitiveCodeAnalysis.Tests.Reporting;

public class CustomReporterLoaderTests
{
    private TempFiles _temp = null!;
    private string _plugins = null!;

    [SetUp]
    public void SetUp()
    {
        _temp = new TempFiles();
        _plugins = Path.Combine(_temp.tmpDirectory, "plugins");
    }

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private string Output => Path.Combine(_temp.tmpDirectory, "out", "report.txt");

    private CognitiveConfiguration ConfigFor(string key, string type, string assembly, bool churn = false)
    {
        var configuration = new CognitiveConfiguration { ConfigDirectory = _temp.tmpDirectory, ScoreThreshold = 1.5 };
        var entries = churn ? configuration.CustomReporters.Churn : configuration.CustomReporters.Cognitive;
        entries[key] = new CustomReporterEntry { Type = type, Assembly = assembly };
        return configuration;
    }

    private static CognitiveMetricsCollection Metrics() =>
    [
        new CognitiveMetrics("Low", "A", "/x/A.cs", "void Low()", 3, ifCount: 1) { totalScore = 1 },
        new CognitiveMetrics("High", "A", "/x/A.cs", "void High()", 9, ifCount: 2) { totalScore = 5, ifScore = 0.5 },
    ];

    private void RunCognitive(CognitiveConfiguration configuration, string key = "k") =>
        CustomReporterLoader.RunCognitive(key, configuration, Output, Metrics(), baseline: null);

    private string CompileCognitive(string source = PluginBuilder.CognitiveReporter, string name = "Plugin") =>
        PluginBuilder.Compile(_plugins, name, source);

    private CustomReporterException Fails(CognitiveConfiguration configuration)
    {
        var exception = Assert.Throws<CustomReporterException>(() => RunCognitive(configuration));
        Assert.That(File.Exists(Output), Is.False, "a failed reporter must not leave an output file");
        return exception!;
    }

    [Test]
    public void Cognitive_RendersTheMethodsAndWritesTheOutputFile()
    {
        CompileCognitive();

        RunCognitive(ConfigFor("k", "Acme.ListingReporter", "plugins/Plugin.dll"));

        Assert.That(File.ReadAllText(Output), Is.EqualTo(
            "threshold=1.5;baseline=False\n"
            + "/x/A.cs|A.High|9|5|ifs=2/0.5|delta=\n"
            + "/x/A.cs|A.Low|3|1|ifs=1/0|delta="));
        Assert.That(Directory.GetFiles(Path.GetDirectoryName(Output)!), Has.Length.EqualTo(1), "no temp file is left");
    }

    [Test]
    public void AssemblyPath_IsRelativeToTheConfigDirectory_NotToTheWorkingDirectory()
    {
        CompileCognitive();
        string previous = Directory.GetCurrentDirectory();
        Directory.SetCurrentDirectory(Path.GetTempPath());
        try
        {
            RunCognitive(ConfigFor("k", "Acme.ListingReporter", "plugins/Plugin.dll"));
        }
        finally
        {
            Directory.SetCurrentDirectory(previous);
        }

        Assert.That(File.Exists(Output), Is.True);
    }

    [Test]
    public void AssemblyPath_AbsolutePathIsUsedAsIs()
    {
        string dll = CompileCognitive();

        RunCognitive(ConfigFor("k", "Acme.ListingReporter", dll));

        Assert.That(File.Exists(Output), Is.True);
    }

    [Test]
    public void AssemblyPath_WithoutConfigDirectoryIsRelativeToTheWorkingDirectory()
    {
        string dll = CompileCognitive();
        var configuration = ConfigFor("k", "Acme.ListingReporter", Path.GetRelativePath(Directory.GetCurrentDirectory(), dll));
        configuration.ConfigDirectory = null;

        RunCognitive(configuration);

        Assert.That(File.Exists(Output), Is.True);
    }

    [Test]
    public void Dependencies_AreLoadedFromThePluginFolder()
    {
        string dependency = PluginBuilder.Compile(_plugins, "Greeter", "namespace Dep; public static class Greeter { public static string Hello() => \"hello from dep\"; }");
        PluginBuilder.Compile(_plugins, "Plugin", """
            using CognitiveCodeAnalysis.Reporting.Spi;
            namespace Acme;
            public class UsesDependency : ICustomCognitiveReporter
            {
                public string Render(CognitiveReportInput input) => Dep.Greeter.Hello();
            }
            """, dependency);

        RunCognitive(ConfigFor("k", "Acme.UsesDependency", "plugins/Plugin.dll"));

        Assert.That(File.ReadAllText(Output), Is.EqualTo("hello from dep"));
    }

    [Test]
    public void Spi_ComesFromTheTool_EvenWhenThePluginFolderHasItsOwnCopy()
    {
        string dll = CompileCognitive();
        File.Copy(typeof(CustomReporterLoader).Assembly.Location, Path.Combine(_plugins, "CognitiveCodeAnalysis.dll"));

        RunCognitive(ConfigFor("k", "Acme.ListingReporter", dll));

        Assert.That(File.Exists(Output), Is.True);
    }

    [Test]
    public void Churn_RendersTheClassesAndWritesTheOutputFile()
    {
        PluginBuilder.Compile(_plugins, "Plugin", PluginBuilder.ChurnReporter);
        var churn = new List<ChurnMetrics>
        {
            new("A", "/x/A.cs", 2, 3, 6, 0.5, 3, ChurnRiskLevel.High),
            new("B", "/x/B.cs", 1, 1, 1),
        };

        CustomReporterLoader.RunChurn(
            "k", ConfigFor("k", "Acme.ClassListing", "plugins/Plugin.dll", churn: true), Output, churn, true, new DateOnly(2024, 1, 2));

        Assert.That(File.ReadAllText(Output), Is.EqualTo(
            "since=2024-01-02;coverage=True\nA|/x/A.cs|3|6|HIGH\nB|/x/B.cs|1|1|"));
    }

    [Test]
    public void Error_AssemblyNotFound()
    {
        var exception = Fails(ConfigFor("k", "Acme.ListingReporter", "plugins/Missing.dll"));

        Assert.That(exception.Message, Does.Contain("'k'").And.Contain("assembly not found").And.Contain("Missing.dll"));
    }

    [Test]
    public void Error_TypeNotFound()
    {
        CompileCognitive();

        var exception = Fails(ConfigFor("k", "Acme.Nope", "plugins/Plugin.dll"));

        Assert.That(exception.Message, Does.Contain("type Acme.Nope not found"));
    }

    [Test]
    public void Error_TypeDoesNotImplementTheInterface()
    {
        CompileCognitive("namespace Acme; public class Plain { }");

        var exception = Fails(ConfigFor("k", "Acme.Plain", "plugins/Plugin.dll"));

        Assert.That(exception.Message, Does.Contain("does not implement").And.Contain("ICustomCognitiveReporter"));
    }

    [Test]
    public void Error_ChurnKeyWithACognitiveReporterIsTheWrongInterface()
    {
        CompileCognitive();
        var configuration = ConfigFor("k", "Acme.ListingReporter", "plugins/Plugin.dll", churn: true);

        var exception = Assert.Throws<CustomReporterException>(() => CustomReporterLoader.RunChurn(
            "k", configuration, Output, [], false, new DateOnly(2024, 1, 1)));

        Assert.That(exception!.Message, Does.Contain("does not implement").And.Contain("ICustomChurnReporter"));
    }

    [TestCase("public class R : ICustomCognitiveReporter { private R() { } public string Render(CognitiveReportInput i) => \"\"; }")]
    [TestCase("public class R : ICustomCognitiveReporter { public R(int x) { } public string Render(CognitiveReportInput i) => \"\"; }")]
    [TestCase("public abstract class R : ICustomCognitiveReporter { public string Render(CognitiveReportInput i) => \"\"; }")]
    public void Error_NoPublicParameterlessConstructor(string declaration)
    {
        CompileCognitive("using CognitiveCodeAnalysis.Reporting.Spi; namespace Acme; " + declaration);

        var exception = Fails(ConfigFor("k", "Acme.R", "plugins/Plugin.dll"));

        Assert.That(exception.Message, Does.Contain("public parameterless constructor"));
    }

    [Test]
    public void Error_ConstructorThrows_ShowsTheRealCause()
    {
        CompileCognitive("""
            using CognitiveCodeAnalysis.Reporting.Spi;
            namespace Acme;
            public class R : ICustomCognitiveReporter
            {
                public R() { throw new System.ArgumentException("bad setup"); }
                public string Render(CognitiveReportInput i) => "";
            }
            """);

        var exception = Fails(ConfigFor("k", "Acme.R", "plugins/Plugin.dll"));

        Assert.That(exception.Message, Does.Contain("constructor of Acme.R threw ArgumentException: bad setup"));
        Assert.That(exception.InnerException, Is.TypeOf<ArgumentException>());
    }

    [Test]
    public void Error_StaticInitializerThrows_ShowsTheRealCause()
    {
        CompileCognitive("""
            using CognitiveCodeAnalysis.Reporting.Spi;
            namespace Acme;
            public class R : ICustomCognitiveReporter
            {
                static readonly int Boom = Fail();
                static int Fail() => throw new System.InvalidOperationException("static boom");
                public string Render(CognitiveReportInput i) => Boom.ToString();
            }
            """);

        var exception = Fails(ConfigFor("k", "Acme.R", "plugins/Plugin.dll"));

        Assert.That(exception.Message, Does.Contain("InvalidOperationException: static boom"));
    }

    [Test]
    public void Error_RenderThrows_AndNoFileIsWritten()
    {
        CompileCognitive("""
            using CognitiveCodeAnalysis.Reporting.Spi;
            namespace Acme;
            public class R : ICustomCognitiveReporter
            {
                public string Render(CognitiveReportInput i) => throw new System.InvalidOperationException("render boom");
            }
            """);

        var exception = Fails(ConfigFor("k", "Acme.R", "plugins/Plugin.dll"));

        Assert.That(exception.Message, Does.Contain("'k'").And.Contain("failed while rendering").And.Contain("render boom"));
    }

    [Test]
    public void Error_RenderReturnsNull()
    {
        CompileCognitive("""
            using CognitiveCodeAnalysis.Reporting.Spi;
            namespace Acme;
            public class R : ICustomCognitiveReporter
            {
                public string Render(CognitiveReportInput i) => null!;
            }
            """);

        var exception = Fails(ConfigFor("k", "Acme.R", "plugins/Plugin.dll"));

        Assert.That(exception.Message, Does.Contain("returned null"));
    }

    [Test]
    public void Error_AssemblyIsNotAManagedAssembly()
    {
        Directory.CreateDirectory(_plugins);
        File.WriteAllText(Path.Combine(_plugins, "Plugin.dll"), "not an assembly");

        var exception = Fails(ConfigFor("k", "Acme.R", "plugins/Plugin.dll"));

        Assert.That(exception.Message, Does.Contain("cannot be loaded"));
    }

    [Test]
    public void Error_DependencyIsMissing()
    {
        string dependency = PluginBuilder.Compile(Path.Combine(_temp.tmpDirectory, "elsewhere"), "Greeter", "namespace Dep; public static class Greeter { public static string Hello() => \"x\"; }");
        PluginBuilder.Compile(_plugins, "Plugin", """
            using CognitiveCodeAnalysis.Reporting.Spi;
            namespace Acme;
            public class R : ICustomCognitiveReporter
            {
                public string Render(CognitiveReportInput i) => Dep.Greeter.Hello();
            }
            """, dependency);

        var exception = Fails(ConfigFor("k", "Acme.R", "plugins/Plugin.dll"));

        Assert.That(exception.Message, Does.Contain("Greeter"));
    }

    [TestCase(null, "plugins/Plugin.dll")]
    [TestCase("Acme.R", null)]
    [TestCase("", "plugins/Plugin.dll")]
    [TestCase("Acme.R", " ")]
    public void Error_TypeOrAssemblyIsMissingInTheConfiguration(string? type, string? assembly)
    {
        var configuration = ConfigFor("k", "x", "y");
        configuration.CustomReporters.Cognitive["k"] = new CustomReporterEntry { Type = type, Assembly = assembly };

        var exception = Fails(configuration);

        Assert.That(exception.Message, Does.Contain("needs both \"type\" and \"assembly\""));
    }

    [Test]
    public void NativeDependencies_AreLoadedFromThePluginFolder_ListedInTheDepsJson()
    {
        string runtimeLibrary = Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "libSystem.Native.so");
        if (!OperatingSystem.IsLinux() || !File.Exists(runtimeLibrary))
        {
            Assert.Ignore("Needs the libSystem.Native.so of the Linux runtime as the native library to load.");
        }

        PluginBuilder.Compile(_plugins, "Plugin", """
            using System.Runtime.InteropServices;
            using CognitiveCodeAnalysis.Reporting.Spi;
            namespace Acme;
            public class Native : ICustomCognitiveReporter
            {
                [DllImport("mynative")] private static extern int SystemNative_GetPid();
                public string Render(CognitiveReportInput input) => SystemNative_GetPid().ToString();
            }
            """);
        string rid = "linux-" + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        Directory.CreateDirectory(Path.Combine(_plugins, "runtimes", rid, "native"));
        File.Copy(runtimeLibrary, Path.Combine(_plugins, "runtimes", rid, "native", "libmynative.so"));
        File.WriteAllText(Path.Combine(_plugins, "Plugin.deps.json"), $$"""
            {
              "runtimeTarget": { "name": ".NETCoreApp,Version=v8.0/{{rid}}" },
              "targets": {
                ".NETCoreApp,Version=v8.0/{{rid}}": {
                  "Plugin/1.0.0": {
                    "runtime": { "Plugin.dll": {} },
                    "runtimeTargets": { "runtimes/{{rid}}/native/libmynative.so": { "rid": "{{rid}}", "assetType": "native" } }
                  }
                }
              },
              "libraries": { "Plugin/1.0.0": { "type": "project", "serviceable": false, "sha512": "" } }
            }
            """);

        RunCognitive(ConfigFor("k", "Acme.Native", "plugins/Plugin.dll"));

        Assert.That(File.ReadAllText(Output), Is.EqualTo(Environment.ProcessId.ToString()));
    }

    [Test]
    public void NativeDependencies_ThatAreNotListedFallBackToTheDefaultProbing()
    {
        PluginBuilder.Compile(_plugins, "Plugin", """
            using System.Runtime.InteropServices;
            using CognitiveCodeAnalysis.Reporting.Spi;
            namespace Acme;
            public class Native : ICustomCognitiveReporter
            {
                [DllImport("does-not-exist-anywhere")] private static extern int Nope();
                public string Render(CognitiveReportInput input) => Nope().ToString();
            }
            """);

        var exception = Fails(ConfigFor("k", "Acme.Native", "plugins/Plugin.dll"));

        Assert.That(exception.Message, Does.Contain("DllNotFoundException"));
    }
}
