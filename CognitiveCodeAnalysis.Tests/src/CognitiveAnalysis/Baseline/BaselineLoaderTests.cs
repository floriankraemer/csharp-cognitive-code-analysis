/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.UnderstandabilityAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Baseline;

public class BaselineLoaderTests
{
    [Test]
    public void RoundTrip_SerializesAndDeserializesSnapshot()
    {
        var metrics = new CognitiveMetrics(
            methodName: "Alpha",
            className: "Demo",
            filePath: "src/Demo.cs",
            methodSignature: "void Alpha()",
            methodLineNumber: 10
        );
        metrics.totalScore = 2.5;

        var snapshot = BaselineSnapshotFactory.FromMetricsCollection(new CognitiveMetricsCollection { metrics });
        var path = Path.Combine(Path.GetTempPath(), "baseline-" + Guid.NewGuid() + ".json");

        try
        {
            File.WriteAllText(path, BaselineLoader.Serialize(snapshot));
            var loaded = BaselineLoader.Load(path);

            Assert.That(loaded.SchemaVersion, Is.EqualTo(CognitiveBaselineSnapshot.CurrentSchemaVersion));
            Assert.That(loaded.Methods, Has.Count.EqualTo(1));
            Assert.That(loaded.Methods[0].MethodName, Is.EqualTo("Alpha"));
            Assert.That(loaded.Methods[0].TotalScore, Is.EqualTo(2.5).Within(0.0001));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void RoundTrip_KeepsTheUnderstandabilityAndItsRiskLevel()
    {
        var metrics = new CognitiveMetrics("Alpha", "Demo", "src/Demo.cs", "void Alpha()", 10,
            understandability: new Understandability(Structural: 4, Hybrid: 1, Fundamental: 2, Nesting: 3, Recursion: 0));
        string json = BaselineLoader.Serialize(BaselineSnapshotFactory.FromMetricsCollection(new CognitiveMetricsCollection { metrics }));
        var path = Path.Combine(Path.GetTempPath(), "baseline-u-" + Guid.NewGuid() + ".json");

        try
        {
            File.WriteAllText(path, json);
            CognitiveBaselineMethodSnapshot loaded = BaselineLoader.Load(path).Methods.Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(json, Does.Contain("\"understandability\": 10").And.Contain("\"understandabilityRiskLevel\": \"medium\""));
                Assert.That(loaded.Understandability, Is.EqualTo(10));
                Assert.That(loaded.UnderstandabilityRiskLevel, Is.EqualTo("medium"));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Load_SchemaVersion1FileWithoutUnderstandability_StillLoads()
    {
        var path = Path.Combine(Path.GetTempPath(), "baseline-old-" + Guid.NewGuid() + ".json");

        try
        {
            File.WriteAllText(path, """
                {"schemaVersion":1,"generatedAt":"2026-01-01T00:00:00Z","methods":[
                  {"filePath":"src/Demo.cs","className":"Demo","methodName":"Alpha","methodSignature":"void Alpha()",
                   "methodLineNumber":10,"totalScore":2.5,"cyclomaticComplexity":3}
                ],"classCoupling":[]}
                """);
            CognitiveBaselineMethodSnapshot loaded = BaselineLoader.Load(path).Methods.Single();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(loaded.TotalScore, Is.EqualTo(2.5));
                Assert.That(loaded.Understandability, Is.Null);
                Assert.That(loaded.UnderstandabilityRiskLevel, Is.Null);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Load_ThrowsForUnsupportedSchemaVersion()
    {
        var path = Path.Combine(Path.GetTempPath(), "baseline-bad-" + Guid.NewGuid() + ".json");

        try
        {
            File.WriteAllText(path, """{"schemaVersion":99,"generatedAt":"2026-01-01T00:00:00Z","methods":[],"classCoupling":[]}""");
            Assert.Throws<InvalidOperationException>(() => BaselineLoader.Load(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Test]
    public void Load_ThrowsWhenFileMissing()
    {
        Assert.Throws<FileNotFoundException>(() => BaselineLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json")));
    }
}
