/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Baseline;

public class BaselineDuplicateKeyTests
{
    private CognitiveCodeAnalyser _analyser = null!;
    private CognitiveConfiguration _configuration = null!;
    private TempFiles _tempFiles = null!;

    [SetUp]
    public void SetUp()
    {
        _analyser = new CognitiveCodeAnalyser();
        _configuration = CognitiveConfigurationDefaults.Create();
        _tempFiles = new TempFiles();
    }

    [TearDown]
    public void TearDown() => _tempFiles.CleanUp();

    [Test]
    public void Compare_WithGenericConstraintOverloads_DoesNotThrow()
    {
        _tempFiles.CreateFileWithContent(
            "Generic.cs",
            """
            namespace DupTest;
            public class GenericOverload
            {
                public void Foo<T>(T t) where T : class { }
                public void Foo<T>(T t) where T : struct { }
            }
            """
        );

        var files = Directory.GetFiles(_tempFiles.tmpDirectory, "*.cs").ToList();
        var metrics = _analyser.AnalyseFilesAsync(files, _configuration).GetAwaiter().GetResult();
        var snapshot = BaselineSnapshotFactory.FromMetricsCollection(metrics);

        Assert.DoesNotThrow(() => BaselineComparer.Compare(metrics, snapshot));

        var duplicateKeys = metrics
            .GroupBy(BaselineMethodKey.FromMetrics)
            .Where(g => g.Count() > 1)
            .ToList();

        Assert.That(duplicateKeys, Is.Empty);
    }

    [Test]
    public void Compare_WithSameClassNameInDifferentNamespaces_DoesNotThrow()
    {
        _tempFiles.CreateFileWithContent(
            "Worker1.cs",
            """
            namespace NamespaceA;
            public class Worker { public void A() { } }
            """
        );
        _tempFiles.CreateFileWithContent(
            "Worker2.cs",
            """
            namespace NamespaceB;
            public class Worker { public void B() { } }
            """
        );

        var files = Directory.GetFiles(_tempFiles.tmpDirectory, "*.cs").ToList();
        var metrics = _analyser.AnalyseFilesAsync(files, _configuration).GetAwaiter().GetResult();
        var snapshot = BaselineSnapshotFactory.FromMetricsCollection(metrics);

        Assert.DoesNotThrow(() => BaselineComparer.Compare(metrics, snapshot));
        Assert.That(metrics.Select(m => m.ClassName).Distinct().Count(), Is.EqualTo(2));
    }
}
