/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

public class CoverageMatcherTests
{
    [Test]
    public void FileLevelAggregate_MatchesByPath_WhenClassNameDiffers()
    {
        string filePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_match.cs"));
        File.WriteAllText(filePath, "//");

        try
        {
            var metrics = new CognitiveMetrics(
                methodName: "DoWork",
                className: "MyApp.Services.Processor",
                filePath: filePath,
                methodSignature: "void DoWork()",
                methodLineNumber: 10
            );

            var collection = new CognitiveMetricsCollection { metrics };

            var fileAggregate = new Coverage
            {
                FullyQualifiedClassName = string.Empty,
                FilePath = filePath,
                MethodName = string.Empty,
                MethodLineNumber = 0,
                LinesCovered = 2,
                LinesTotal = 5,
                BranchesCovered = 0,
                BranchesTotal = 0
            };

            Dictionary<CognitiveMetrics, Coverage> matches = CoverageMatcher.MatchCoverageToMetrics(
                collection,
                new[] { fileAggregate });

            Assert.That(matches, Has.Count.EqualTo(1));
            Assert.That(matches[metrics].LinesTotal, Is.EqualTo(5));
            Assert.That(matches[metrics].LinesCovered, Is.EqualTo(2));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }

    [Test]
    public void ClassLevelMatch_UsedWhenNoMethodMatch()
    {
        string filePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_class.cs"));
        File.WriteAllText(filePath, "//");

        try
        {
            var metrics = new CognitiveMetrics(
                methodName: "Compute",
                className: "MyApp.Engine",
                filePath: filePath,
                methodSignature: "void Compute()",
                methodLineNumber: 5
            );
            var collection = new CognitiveMetricsCollection { metrics };

            var classCov = new Coverage
            {
                FullyQualifiedClassName = "MyApp.Engine",
                FilePath = filePath,
                MethodName = string.Empty,
                MethodLineNumber = 0,
                LinesCovered = 7,
                LinesTotal = 10,
            };

            var matches = CoverageMatcher.MatchCoverageToMetrics(collection, [classCov]);

            Assert.That(matches, Has.Count.EqualTo(1));
            Assert.That(matches[metrics].LinesTotal, Is.EqualTo(10));
        }
        finally
        {
            if (File.Exists(filePath)) File.Delete(filePath);
        }
    }

    [Test]
    public void MatchCoverageToMetrics_WithLargeSets_CompletesQuickly()
    {
        var metrics = Enumerable.Range(0, 5000).Select(i => new CognitiveMetrics(
            methodName: $"Method{i}",
            className: $"NS.Class{i}",
            filePath: $"/src/Class{i}.cs",
            methodSignature: $"void Method{i}()",
            methodLineNumber: 1
        )).ToList();

        var coverage = Enumerable.Range(0, 5000).Select(i => new Coverage
        {
            FullyQualifiedClassName = $"NS.Class{i}",
            FilePath = $"/src/Class{i}.cs",
            MethodName = $"Method{i}",
            MethodLineNumber = 1,
            LinesCovered = 5,
            LinesTotal = 10,
        }).ToList();

        var collection = new CognitiveMetricsCollection();
        foreach (var m in metrics) collection.Add(m);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var matches = CoverageMatcher.MatchCoverageToMetrics(collection, coverage);
        sw.Stop();

        Assert.That(matches, Has.Count.EqualTo(5000));
        Assert.That(sw.ElapsedMilliseconds, Is.LessThan(3000),
            $"Matching 5000x5000 took {sw.ElapsedMilliseconds} ms; should be well under 3 s with O(N+M) indexing.");
    }

    [Test]
    public void MethodLevelMatch_TakesPrecedence_OverFileAggregate()
    {
        string filePath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_prec.cs"));
        File.WriteAllText(filePath, "//");

        try
        {
            var metrics = new CognitiveMetrics(
                methodName: "M",
                className: "C",
                filePath: filePath,
                methodSignature: "void M()",
                methodLineNumber: 3
            );

            var collection = new CognitiveMetricsCollection { metrics };

            var methodCov = new Coverage
            {
                FullyQualifiedClassName = "C",
                FilePath = filePath,
                MethodName = "M",
                MethodLineNumber = 3,
                LinesCovered = 10,
                LinesTotal = 10,
                BranchesCovered = 0,
                BranchesTotal = 0
            };

            var fileAgg = new Coverage
            {
                FullyQualifiedClassName = string.Empty,
                FilePath = filePath,
                MethodName = string.Empty,
                MethodLineNumber = 0,
                LinesCovered = 1,
                LinesTotal = 100,
                BranchesCovered = 0,
                BranchesTotal = 0
            };

            Dictionary<CognitiveMetrics, Coverage> matches = CoverageMatcher.MatchCoverageToMetrics(
                collection,
                new[] { fileAgg, methodCov });

            Assert.That(matches[metrics].LinesTotal, Is.EqualTo(10));
        }
        finally
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
    }
}
