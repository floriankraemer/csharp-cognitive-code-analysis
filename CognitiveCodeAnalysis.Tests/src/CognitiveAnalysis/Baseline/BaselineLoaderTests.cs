/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;

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
