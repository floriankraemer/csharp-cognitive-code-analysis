/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.Json;

using CognitiveCodeAnalysis.Cache;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Common;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.Cache;

public class MetricsCacheTests
{
    private const string Rich = """
        namespace Samples;

        public class Rich
        {
            private int _count;
            public int Total { get; set; }

            public int Work(int a, int b, Other other)
            {
                var sum = 0;
                if (a > 0 && b > 0) { sum += a; } else { sum -= b; }
                foreach (var i in new[] { a, b }) { if (i > 1) { sum += i; } }
                try { _count++; Total += other.Value; }
                catch (System.Exception e) { return e.Message.Length; }
                return a > b ? sum : _count;
            }
        }
        """;

    private const string OtherWithField = "namespace Samples; public class Other { public int Value; }";
    private const string OtherWithProperty = "namespace Samples; public class Other { public int Value { get; set; } }";

    private string _dir = null!;
    private string _cacheDir = null!;
    private readonly CognitiveCodeAnalyser _analyser = new();

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cca-cache-" + Guid.NewGuid().ToString("N"));
        _cacheDir = Path.Combine(_dir, "cache");
        Directory.CreateDirectory(_dir);
        Write("Rich.cs", Rich);
        Write("Other.cs", OtherWithField);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    private string Write(string name, string content)
    {
        string path = Path.Combine(_dir, "src", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private CognitiveConfiguration Config(bool cache = true) => new()
    {
        Cache = new CacheSettings { Enabled = cache, Directory = _cacheDir },
    };

    private CognitiveMetricsCollection Analyse(CognitiveConfiguration configuration, Action<string>? warn = null)
    {
        List<string> files = Directory.GetFiles(Path.Combine(_dir, "src"), "*.cs").Order(StringComparer.Ordinal).ToList();
        CompiledSourceSet sources = CompiledSourceSet.BuildAsync(files).GetAwaiter().GetResult();
        return _analyser.AnalyseCompiled(sources, configuration, progress: null, warn: warn);
    }

    private static string Snapshot(CognitiveMetricsCollection metrics) =>
        JsonSerializer.Serialize(metrics, new JsonSerializerOptions { IncludeFields = true });

    private string[] Entries() =>
        Directory.Exists(_cacheDir) ? Directory.GetFiles(_cacheDir, "*.json", SearchOption.AllDirectories) : [];

    private void Tamper(string from, string to)
    {
        foreach (string entry in Entries())
        {
            File.WriteAllText(entry, File.ReadAllText(entry).Replace(from, to));
        }
    }

    [Test]
    public void Disabled_ByDefault_CreatesNothing()
    {
        Assert.That(new CognitiveConfiguration().Cache.Enabled, Is.False);

        Analyse(Config(cache: false));

        Assert.That(Directory.Exists(_cacheDir), Is.False);
    }

    [Test]
    public void Begin_WhenDisabled_ReturnsNull()
    {
        var cache = new MetricsCache(new CacheSettings { Enabled = false }, _dir);
        CompiledSourceSet sources = CompiledSourceSet.BuildAsync([]).GetAwaiter().GetResult();

        Assert.That(cache.Begin(sources, new CognitiveConfiguration()), Is.Null);
    }

    [Test]
    public void Hit_ReturnsResultsIdenticalToAFreshAnalysis_InEveryField()
    {
        string fresh = Snapshot(Analyse(Config(cache: false)));

        string firstCachedRun = Snapshot(Analyse(Config()));
        string hit = Snapshot(Analyse(Config()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Entries(), Has.Length.EqualTo(2));
            Assert.That(firstCachedRun, Is.EqualTo(fresh));
            Assert.That(hit, Is.EqualTo(fresh));
            Assert.That(fresh, Does.Contain("\"Volume\":").And.Contain("\"Structural\":"));
        }
    }

    [Test]
    public void Hit_ReadsTheStoredEntry()
    {
        Analyse(Config());
        Tamper("\"MethodName\":\"Work\"", "\"MethodName\":\"FromCache\"");

        Assert.That(Analyse(Config()).Select(m => m.MethodName), Does.Contain("FromCache"));
    }

    [Test]
    public void Hit_RestoresTheFilePathOfTheCurrentRun()
    {
        Analyse(Config());

        Assert.That(Analyse(Config()).Select(m => m.FilePath), Has.All.StartWith(_dir));
    }

    [Test]
    public void Entries_AreWrittenAtomically_WithoutLeftoversAndWithNormalPermissions()
    {
        Analyse(Config());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.GetFiles(_cacheDir, "*.tmp", SearchOption.AllDirectories), Is.Empty);
            Assert.That(Entries(), Is.Not.Empty);
            if (!OperatingSystem.IsWindows())
            {
                UnixFileMode mode = File.GetUnixFileMode(Entries()[0]);
                Assert.That(mode & (UnixFileMode.UserRead | UnixFileMode.UserWrite), Is.EqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite));
                Assert.That(mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute), Is.EqualTo(UnixFileMode.None));
            }
        }
    }

    [Test]
    public void Miss_WhenTheFileContentChanges()
    {
        Analyse(Config());
        Tamper("\"MethodName\":\"Work\"", "\"MethodName\":\"FromCache\"");
        Write("Rich.cs", Rich + "\n// edited");

        Assert.That(Analyse(Config()).Select(m => m.MethodName), Does.Not.Contain("FromCache"));
    }

    [Test]
    public void Miss_WhenAnAnalysisSettingChanges()
    {
        Analyse(Config());
        Tamper("\"MethodName\":\"Work\"", "\"MethodName\":\"FromCache\"");
        CognitiveConfiguration changed = Config();
        changed.CountElseAsNesting = true;

        Assert.That(Analyse(changed).Select(m => m.MethodName), Does.Not.Contain("FromCache"));
    }

    [Test]
    public void Miss_WhenAnExcludePatternChanges()
    {
        Analyse(Config());
        CognitiveConfiguration changed = Config();
        changed.ExcludePatterns = ["Work"];

        Assert.That(Analyse(changed).Select(m => m.MethodName), Does.Not.Contain("Work"));
    }

    [Test]
    public void Miss_WhenAnyOtherSettingChanges_SoNoFutureSettingCanGiveAStaleHit()
    {
        Analyse(Config());
        Tamper("\"MethodName\":\"Work\"", "\"MethodName\":\"FromCache\"");
        CognitiveConfiguration threshold = Config();
        threshold.ScoreThreshold = 9;
        CognitiveConfiguration display = Config();
        display.ShowHalsteadComplexity = true;
        CognitiveConfiguration metric = Config();
        metric.Metrics["ifCount"] = new MetricConfiguration { Threshold = 7 };

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Analyse(threshold).Select(m => m.MethodName), Does.Not.Contain("FromCache"));
            Assert.That(Analyse(display).Select(m => m.MethodName), Does.Not.Contain("FromCache"));
            Assert.That(Analyse(metric).Select(m => m.MethodName), Does.Not.Contain("FromCache"));
        }
    }

    [Test]
    public void Hit_WhenOnlyTheOrderOfTheMetricsDiffers()
    {
        CognitiveConfiguration a = Config();
        a.Metrics["ifCount"] = new MetricConfiguration { Threshold = 1 };
        a.Metrics["elseCount"] = new MetricConfiguration { Threshold = 2 };
        Analyse(a);
        Tamper("\"MethodName\":\"Work\"", "\"MethodName\":\"FromCache\"");
        CognitiveConfiguration b = Config();
        b.Metrics["elseCount"] = new MetricConfiguration { Threshold = 2 };
        b.Metrics["ifCount"] = new MetricConfiguration { Threshold = 1 };

        Assert.That(Analyse(b).Select(m => m.MethodName), Does.Contain("FromCache"));
    }

    [Test]
    public void Miss_WhenTheToolVersionChanges()
    {
        CompiledSourceSet sources = CompiledSourceSet.BuildAsync([Write("Rich.cs", Rich)]).GetAwaiter().GetResult();
        var settings = new CacheSettings { Enabled = true, Directory = _cacheDir };

        string v1 = new MetricsCache(settings, _dir, null, "1.0.0").Begin(sources, Config())!.KeyOf(sources.SyntaxTrees[0]);
        string v1Again = new MetricsCache(settings, _dir, null, "1.0.0").Begin(sources, Config())!.KeyOf(sources.SyntaxTrees[0]);
        string v2 = new MetricsCache(settings, _dir, null, "1.0.1").Begin(sources, Config())!.KeyOf(sources.SyntaxTrees[0]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(v1Again, Is.EqualTo(v1));
            Assert.That(v2, Is.Not.EqualTo(v1));
        }
    }

    [Test]
    public void Miss_WhenAnotherFileOfTheRunChanges_BecauseTheSemanticsCanChange()
    {
        // Rich.cs does not change, but its field/property counts depend on Other.cs.
        MethodCounts(Analyse(Config()), out int fieldsBefore, out int propertiesBefore);

        Write("Other.cs", OtherWithProperty);
        CognitiveMetricsCollection afterChange = Analyse(Config());
        MethodCounts(afterChange, out int fieldsAfter, out int propertiesAfter);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(fieldsAfter, Is.EqualTo(fieldsBefore - 1));
            Assert.That(propertiesAfter, Is.EqualTo(propertiesBefore + 1));
            Assert.That(Snapshot(afterChange), Is.EqualTo(Snapshot(Analyse(Config(cache: false)))));
        }
    }

    private static void MethodCounts(CognitiveMetricsCollection metrics, out int fields, out int properties)
    {
        CognitiveMetrics work = metrics.Single(m => m.MethodName == "Work");
        fields = work.fieldAccessCount;
        properties = work.propertyAccessCount;
    }

    [Test]
    public void Miss_AndOverwrite_WhenTheEntryIsCorrupt()
    {
        string fresh = Snapshot(Analyse(Config()));
        foreach (string entry in Entries())
        {
            File.WriteAllText(entry, "{ this is not json");
        }

        string recovered = Snapshot(Analyse(Config()));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(recovered, Is.EqualTo(fresh));
            Assert.That(Entries().Select(File.ReadAllText), Has.All.StartWith("{\"Version\":"));
        }
    }

    [TestCase("{\"Version\":1,\"Methods\":[null]}")]
    [TestCase("{\"Version\":1,\"Methods\":null}")]
    [TestCase("null")]
    [TestCase("")]
    public void Miss_WhenTheEntryIsIncomplete(string content)
    {
        string fresh = Snapshot(Analyse(Config()));
        foreach (string entry in Entries())
        {
            File.WriteAllText(entry, content);
        }

        Assert.That(Snapshot(Analyse(Config())), Is.EqualTo(fresh));
    }

    [Test]
    public void Miss_WhenOneMethodOfTheEntryIsMissing()
    {
        string fresh = Snapshot(Analyse(Config()));
        foreach (string entry in Entries())
        {
            File.WriteAllText(entry, File.ReadAllText(entry).Replace("\"Methods\":[", "\"Methods\":[null,"));
        }

        Assert.That(Snapshot(Analyse(Config())), Is.EqualTo(fresh));
    }

    [Test]
    public void Entries_HaveNoByteOrderMark()
    {
        Analyse(Config());

        Assert.That(Entries().Select(entry => File.ReadAllBytes(entry)[0]), Has.All.EqualTo((byte)'{'));
    }

    [Test]
    public void Miss_AndOverwrite_WhenTheEntryHasAnotherFormatVersion()
    {
        Analyse(Config());
        Tamper("\"MethodName\":\"Work\"", "\"MethodName\":\"FromCache\"");
        Tamper("\"Version\":" + MetricsCache.FormatVersion, "\"Version\":" + (MetricsCache.FormatVersion + 1));

        CognitiveMetricsCollection result = Analyse(Config());

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Select(m => m.MethodName), Does.Not.Contain("FromCache"));
            Assert.That(Entries().Select(File.ReadAllText), Has.All.StartWith("{\"Version\":" + MetricsCache.FormatVersion + ","));
        }
    }

    [Test]
    public void UnwritableDirectory_WarnsOnce_AndTheAnalysisContinues()
    {
        Write("Third.cs", "namespace Samples; public class Third { public int Run() { return 1; } }");
        string blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "a file where a directory is needed");
        CognitiveConfiguration configuration = Config();
        configuration.Cache.Directory = Path.Combine(blocker, "cache");
        var warnings = new System.Collections.Concurrent.ConcurrentQueue<string>();

        string result = Snapshot(Analyse(configuration, warnings.Enqueue));

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result, Is.EqualTo(Snapshot(Analyse(Config(cache: false)))));
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(warnings.Single(), Does.Contain("cannot be written").And.Contain(configuration.Cache.Directory));
        }
    }

    [Test]
    public void FailedMove_WarnsAndLeavesNoTemporaryFile()
    {
        CompiledSourceSet sources = CompiledSourceSet.BuildAsync([Write("Rich.cs", Rich)]).GetAwaiter().GetResult();
        var settings = new CacheSettings { Enabled = true, Directory = _cacheDir };
        var warnings = new List<string>();
        MetricsCache.Run run = new MetricsCache(settings, _dir, warnings.Add).Begin(sources, Config())!;
        // A directory where the entry belongs makes the final move fail after the temporary file was written.
        string entry = Path.Combine(_cacheDir, run.KeyOf(sources.SyntaxTrees[0])[..2], run.KeyOf(sources.SyntaxTrees[0]) + ".json");
        Directory.CreateDirectory(entry);

        run.Store(sources.SyntaxTrees[0], []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(warnings, Has.Count.EqualTo(1));
            Assert.That(Directory.GetFiles(_cacheDir, "*.tmp", SearchOption.AllDirectories), Is.Empty);
        }
    }

    [Test]
    public void UnwritableDirectory_WithoutWarningCallback_DoesNotThrow()
    {
        string blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");
        CognitiveConfiguration configuration = Config();
        configuration.Cache.Directory = Path.Combine(blocker, "cache");

        Assert.That(() => Analyse(configuration), Throws.Nothing);
    }

    [Test]
    public void RelativeDirectory_ResolvesAgainstTheWorkingDirectory_AndBlankFallsBackToTheDefault()
    {
        CompiledSourceSet sources = CompiledSourceSet.BuildAsync([Write("Rich.cs", Rich)]).GetAwaiter().GetResult();

        new MetricsCache(new CacheSettings { Enabled = true, Directory = "rel/cache" }, _dir)
            .Begin(sources, Config())!.Store(sources.SyntaxTrees[0], []);
        new MetricsCache(new CacheSettings { Enabled = true, Directory = "  " }, _dir)
            .Begin(sources, Config())!.Store(sources.SyntaxTrees[0], []);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.GetFiles(Path.Combine(_dir, "rel", "cache"), "*.json", SearchOption.AllDirectories), Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(Path.Combine(_dir, ".cca", "cache"), "*.json", SearchOption.AllDirectories), Has.Length.EqualTo(1));
        }
    }

    [Test]
    public void Settings_DefaultToDisabled_AndLoadFromTheConfigFile()
    {
        string file = Path.Combine(_dir, "cfg.json");
        File.WriteAllText(file, """{ "cognitive": { "cache": { "enabled": true, "directory": "x/y" } } }""");

        CacheSettings loaded = ConfigurationLoader.Load(file).Cache;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(CognitiveConfigurationDefaults.Create().Cache.Enabled, Is.False);
            Assert.That(CognitiveConfigurationDefaults.Create().Cache.Directory, Is.EqualTo(".cca/cache"));
            Assert.That(loaded.Enabled, Is.True);
            Assert.That(loaded.Directory, Is.EqualTo("x/y"));
        }
    }
}
