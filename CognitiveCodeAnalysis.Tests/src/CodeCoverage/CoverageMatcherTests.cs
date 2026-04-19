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
