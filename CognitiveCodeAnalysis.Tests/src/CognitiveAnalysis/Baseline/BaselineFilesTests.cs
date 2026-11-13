/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Baseline;

public class BaselineFilesTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private string Dir => Path.Combine(_temp.tmpDirectory, ".cca", "baseline");

    private void Touch(string name)
    {
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Path.Combine(Dir, name), "{}");
    }

    private string? LatestName() => BaselineFiles.Latest(_temp.tmpDirectory) is { } path ? Path.GetFileName(path) : null;

    [Test]
    public void Latest_WithoutTheDirectory_IsNull() => Assert.That(LatestName(), Is.Null);

    [Test]
    public void Latest_WithoutAnyBaseline_IsNull()
    {
        Directory.CreateDirectory(Dir);

        Assert.That(LatestName(), Is.Null);
    }

    [Test]
    public void Latest_IsTheNewestTimestamp()
    {
        Touch("baseline-2026-01-01_10-00-00.json");
        Touch("baseline-2026-11-12_21-35-02.json");
        Touch("baseline-2026-03-01_00-00-00.json");

        Assert.That(LatestName(), Is.EqualTo("baseline-2026-11-12_21-35-02.json"));
    }

    [Test]
    public void Latest_OrdersTheSuffixNumerically_AndAfterTheUnsuffixedName()
    {
        Touch("baseline-2026-11-12_21-35-02.json");
        Touch("baseline-2026-11-12_21-35-02-2.json");
        Touch("baseline-2026-11-12_21-35-02-10.json");

        Assert.That(LatestName(), Is.EqualTo("baseline-2026-11-12_21-35-02-10.json"));
    }

    [Test]
    public void Latest_OrdersByTimestampBeforeSuffix()
    {
        Touch("baseline-2026-11-12_21-35-02-7.json");
        Touch("baseline-2026-11-12_21-35-03.json");

        Assert.That(LatestName(), Is.EqualTo("baseline-2026-11-12_21-35-03.json"));
    }

    [TestCase("baseline-2026-13-01_00-00-00.json")]
    [TestCase("baseline-2026-02-30_00-00-00.json")]
    [TestCase("baseline-2026-11-12_25-00-00.json")]
    [TestCase("baseline-2026-11-12_21-35-02.json.bak")]
    [TestCase("baseline-2026-11-12_21-35-02-x.json")]
    [TestCase("baseline-2026-11-12_21-35-02-99999999999.json")]
    [TestCase("baseline-26-11-12_21-35-02.json")]
    [TestCase("my-baseline-2026-11-12_21-35-02.json")]
    [TestCase("baseline.json")]
    public void Latest_IgnoresNamesThatAreNotBaselines(string name)
    {
        Touch("baseline-2020-01-01_00-00-00.json");
        Touch(name);

        Assert.That(LatestName(), Is.EqualTo("baseline-2020-01-01_00-00-00.json"));
    }

    [Test]
    public void Latest_IgnoresDirectories()
    {
        Touch("baseline-2020-01-01_00-00-00.json");
        Directory.CreateDirectory(Path.Combine(Dir, "baseline-2026-11-12_21-35-02.json"));

        Assert.That(LatestName(), Is.EqualTo("baseline-2020-01-01_00-00-00.json"));
    }

    [Test]
    public void Generate_WritesUtcNamedFile_AndCreatesTheDirectory()
    {
        var snapshot = new CognitiveBaselineSnapshot
        {
            GeneratedAt = new DateTimeOffset(2026, 11, 12, 23, 35, 2, TimeSpan.FromHours(2)),
            ConfigHash = "h",
        };

        string relative = BaselineFiles.Generate(_temp.tmpDirectory, snapshot);

        Assert.That(relative, Is.EqualTo(Path.Combine(".cca", "baseline", "baseline-2026-11-12_21-35-02.json")));
        var loaded = BaselineLoader.Load(Path.Combine(_temp.tmpDirectory, relative));
        Assert.That(loaded.ConfigHash, Is.EqualTo("h"));
    }

    [Test]
    public void Generate_NeverOverwrites_ItAppendsASuffix()
    {
        var snapshot = new CognitiveBaselineSnapshot { GeneratedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero) };

        var names = Enumerable.Range(0, 3)
            .Select(_ => Path.GetFileName(BaselineFiles.Generate(_temp.tmpDirectory, snapshot)))
            .ToList();

        Assert.That(names, Is.EqualTo(new[]
        {
            "baseline-2026-01-02_03-04-05.json",
            "baseline-2026-01-02_03-04-05-1.json",
            "baseline-2026-01-02_03-04-05-2.json",
        }));
        Assert.That(LatestName(), Is.EqualTo("baseline-2026-01-02_03-04-05-2.json"));
    }

    [Test]
    public void Generate_KeepsAnExistingFileUntouched()
    {
        Touch("baseline-2026-01-02_03-04-05.json");

        BaselineFiles.Generate(
            _temp.tmpDirectory,
            new CognitiveBaselineSnapshot { GeneratedAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero) });

        Assert.That(File.ReadAllText(Path.Combine(Dir, "baseline-2026-01-02_03-04-05.json")), Is.EqualTo("{}"));
    }

    [Test]
    public void Generate_FailsWhenTheDirectoryCannotBeCreated()
    {
        File.WriteAllText(Path.Combine(_temp.tmpDirectory, ".cca"), "a file where the directory belongs");

        Assert.Catch<IOException>(() => BaselineFiles.Generate(_temp.tmpDirectory, new CognitiveBaselineSnapshot()));
    }
}

public class BaselineConfigHashTests
{
    [Test]
    public void Of_IsStableAndIndependentOfInsertionOrder()
    {
        var a = new CognitiveConfiguration
        {
            Metrics = new() { ["ifCount"] = new() { Threshold = 1, Scale = 0.5, Enabled = true }, ["loop"] = new() { Threshold = 2, Scale = 1, Enabled = false } },
        };
        var b = new CognitiveConfiguration
        {
            Metrics = new() { ["loop"] = new() { Threshold = 2, Scale = 1, Enabled = false }, ["ifCount"] = new() { Threshold = 1, Scale = 0.5, Enabled = true } },
        };

        Assert.That(BaselineConfigHash.Of(a), Is.EqualTo(BaselineConfigHash.Of(b)));
        Assert.That(BaselineConfigHash.Of(a), Does.Match("^[0-9a-f]{64}$"));
    }

    [TestCase(2, 0.5, true)]
    [TestCase(1, 0.6, true)]
    [TestCase(1, 0.5, false)]
    public void Of_ChangesWithAnyMetricSetting(int threshold, double scale, bool enabled)
    {
        var original = new CognitiveConfiguration { Metrics = new() { ["ifCount"] = new() { Threshold = 1, Scale = 0.5, Enabled = true } } };
        var changed = new CognitiveConfiguration { Metrics = new() { ["ifCount"] = new() { Threshold = threshold, Scale = scale, Enabled = enabled } } };

        Assert.That(BaselineConfigHash.Of(changed), Is.Not.EqualTo(BaselineConfigHash.Of(original)));
    }

    [Test]
    public void Of_IgnoresSettingsThatDoNotScore()
    {
        var a = CognitiveConfigurationDefaults.Create();
        var b = CognitiveConfigurationDefaults.Create();
        b.ShowCouplingMetrics = !a.ShowCouplingMetrics;

        Assert.That(BaselineConfigHash.Of(a), Is.EqualTo(BaselineConfigHash.Of(b)));
    }

    [Test]
    public void Warning_NamesBothHashes_WhenTheyDiffer() =>
        Assert.That(
            BaselineConfigHash.Warning("aaa", "bbb"),
            Is.EqualTo("Baseline config hash (aaa) does not match current config hash (bbb). Metrics comparison may not be accurate."));

    [Test]
    public void Warning_IsNullWhenTheHashesMatchOrTheBaselineHasNone()
    {
        Assert.That(BaselineConfigHash.Warning("aaa", "aaa"), Is.Null);
        Assert.That(BaselineConfigHash.Warning(null, "aaa"), Is.Null);
    }
}

public class BaselineValidationTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private (string Path, string Message) Reject(string json)
    {
        string path = _temp.CreateFileWithContent("b.json", json);
        var ex = Assert.Throws<InvalidOperationException>(() => BaselineLoader.Load(path));
        return (path, ex!.Message);
    }

    [Test]
    public void Load_CollectsEveryError()
    {
        var (path, message) = Reject("""
            {"methods": [{"filePath": 1, "totalScore": -1, "ifCount": 1.5}, {"className": null}, 7],
             "classCoupling": {"x": 1}}
            """);

        Assert.That(message, Is.EqualTo(
            $"Invalid baseline file format in {path}: "
            + "methods[0].filePath must be a string, "
            + "methods[0].totalScore must be a finite, non-negative number, "
            + "methods[0].ifCount must be a non-negative integer, "
            + "methods[1].className must be a string, "
            + "methods[2] must be an object, "
            + "classCoupling must be an array"));
    }

    [TestCase("""{"methods": [{"totalScore": 1e999}]}""", "methods[0].totalScore must be a finite, non-negative number")]
    [TestCase("""{"methods": [{"totalScore": "1"}]}""", "methods[0].totalScore must be a finite, non-negative number")]
    [TestCase("""{"methods": [{"halsteadVolume": -0.5}]}""", "methods[0].halsteadVolume must be a finite, non-negative number")]
    [TestCase("""{"methods": [{"halsteadVolume": "x"}]}""", "methods[0].halsteadVolume must be a finite, non-negative number")]
    [TestCase("""{"methods": [{"linesOfCode": -1}]}""", "methods[0].linesOfCode must be a non-negative integer")]
    [TestCase("""{"methods": [{"linesOfCode": true}]}""", "methods[0].linesOfCode must be a non-negative integer")]
    [TestCase("""{"classCoupling": [{"stability": -1}]}""", "classCoupling[0].stability must be a finite, non-negative number")]
    [TestCase("""{"classCoupling": [{"className": 5}]}""", "classCoupling[0].className must be a string")]
    [TestCase("""{"methods": null}""", "methods must be an array")]
    [TestCase("""{"generatedAt": "yesterday"}""", "generatedAt must be a date and time")]
    [TestCase("""{"generatedAt": 5}""", "generatedAt must be a date and time")]
    [TestCase("""{"configHash": 5}""", "configHash must be a string")]
    [TestCase("""{"schemaVersion": "one"}""", "schemaVersion must be a non-negative integer")]
    [TestCase("""[1]""", "the root must be a JSON object")]
    [TestCase("""7""", "the root must be a JSON object")]
    public void Load_RejectsInvalidFields(string json, string expectedError)
    {
        var (path, message) = Reject(json);

        Assert.That(message, Is.EqualTo($"Invalid baseline file format in {path}: {expectedError}"));
    }

    [Test]
    public void Load_RejectsMalformedJson()
    {
        var (path, message) = Reject("{not json");

        Assert.That(message, Does.StartWith($"Invalid baseline file format in {path}: "));
    }

    [Test]
    public void Load_ReportsANewerSchemaVersionBeforeOtherProblems()
    {
        var (_, message) = Reject("""{"schemaVersion": 2, "methods": "other layout"}""");

        Assert.That(message, Is.EqualTo("Unsupported baseline schema version 2. Expected version 1."));
    }

    [Test]
    public void Load_StillAcceptsWhatTheOldLoaderAccepted()
    {
        string path = _temp.CreateFileWithContent("ok.json", """
            {
              "generatedAt": "2026-01-01T00:00:00Z",
              "unknownField": {"nested": [1, "x"]},
              "methods": [{
                "filePath": "a.cs", "className": "C", "methodSignature": "void M()", "unknownMetric": -5,
                "totalScore": 0, "halsteadVolume": null, "churnScore": 2.5, "ifCount": 0
              }],
              "classCoupling": [{"className": "C", "incomingCoupling": 1, "stability": 0.5}]
            }
            """);

        var snapshot = BaselineLoader.Load(path);

        Assert.That(snapshot.SchemaVersion, Is.EqualTo(1));
        Assert.That(snapshot.ConfigHash, Is.Null);
        Assert.That(snapshot.Methods.Single().ChurnScore, Is.EqualTo(2.5));
        Assert.That(snapshot.Methods.Single().HalsteadVolume, Is.Null);
    }

    [Test]
    public void Load_AcceptsEmptyObject() =>
        Assert.That(BaselineLoader.Load(_temp.CreateFileWithContent("e.json", "{}")).Methods, Is.Empty);

    [Test]
    public void Load_ReadsTheConfigHash() =>
        Assert.That(BaselineLoader.Load(_temp.CreateFileWithContent("h.json", """{"configHash": "abc"}""")).ConfigHash, Is.EqualTo("abc"));
}
