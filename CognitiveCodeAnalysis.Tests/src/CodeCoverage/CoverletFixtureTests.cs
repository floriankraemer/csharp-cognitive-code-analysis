/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Common;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

/// <summary>
/// Uses a report written by coverlet (the tool this repository's own CI runs) for ChurnCalculator.cs, trimmed to that
/// class, together with the exact source file it was written for.
/// </summary>
public class CoverletFixtureTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private static string Fixture(string name)
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "fixtures", "coverlet", name));

    /// <summary>Lays the source out as the report expects it: &lt;source dir&gt;/CognitiveAnalysis/ChurnCalculator.cs.</summary>
    private (string Coverage, string Source) Arrange()
    {
        string sourceDir = Path.Combine(_temp.tmpDirectory, "src");
        string source = Path.Combine(sourceDir, "CognitiveAnalysis", "ChurnCalculator.cs");
        Directory.CreateDirectory(Path.GetDirectoryName(source)!);
        File.Copy(Fixture("ChurnCalculator.cs"), source);

        string xml = File.ReadAllText(Fixture("churn-calculator.coverlet.cobertura.xml")).Replace("__SOURCE_DIR__", sourceDir, StringComparison.Ordinal);
        return (_temp.CreateFileWithContent("coverage.cobertura.xml", xml), source);
    }

    [Test]
    public void Reader_ReadsCoverletRatesAndFirstLines()
    {
        var (coverage, source) = Arrange();

        var list = new CoberturaReader().ReadCoverage(coverage).ToList();

        var churn = list.Single(c => c.MethodName == "CalculateChurnScore");
        var factor = list.Single(c => c.MethodName == "GetCoverageFactor");
        var cls = list.Single(c => !c.IsMethodLevel);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(cls.FullyQualifiedClassName, Is.EqualTo("CognitiveCodeAnalysis.CognitiveAnalysis.ChurnCalculator"));
            Assert.That(Path.GetFullPath(cls.FilePath), Is.EqualTo(Path.GetFullPath(source)));
            Assert.That(churn.MethodLineNumber, Is.EqualTo(38));
            Assert.That(churn.LinesTotal, Is.EqualTo(0), "coverlet writes no line counts");
            Assert.That(churn.LineCoveragePercentage, Is.EqualTo(100.0));
            Assert.That(churn.BranchCoveragePercentage, Is.Null, "no branch lines means no branch data, although branch-rate is 1");
            Assert.That(factor.MethodLineNumber, Is.EqualTo(52));
            Assert.That(factor.BranchCoveragePercentage, Is.EqualTo(100.0));
        }
    }

    [Test]
    public void Facade_MatchesEachMethodByNameAndFirstCoverageLine_AndKeepsBranchDataOnlyWhereItExists()
    {
        var (coverage, source) = Arrange();
        var facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(),
            new CognitiveCodeAnalyser(),
            CognitiveConfigurationDefaults.Create(),
            new ScoreCalculator(),
            new CoberturaReader(),
            new ClassCouplingAnalyser());
        var metrics = facade.AnalyseSourceFiles([source]);

        var result = facade.LoadCoverageData(coverage, metrics);

        var churn = metrics.Single(m => m.MethodName == "CalculateChurnScore");
        var factor = metrics.Single(m => m.MethodName == "GetCoverageFactor");
        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WarningMessage, Is.Null, "every method found its coverage");
            Assert.That(churn.methodLineNumber, Is.LessThan(38), "the declaration precedes the first covered line");
            Assert.That(churn.lineCoveragePercentage, Is.EqualTo(100.0));
            Assert.That(churn.branchCoveragePercentage, Is.Null);
            Assert.That(churn.churnScore, Is.EqualTo(0.0));
            Assert.That(factor.lineCoveragePercentage, Is.EqualTo(100.0));
            Assert.That(factor.branchCoveragePercentage, Is.EqualTo(100.0));
        }
    }

    [Test]
    public void Facade_CoverletReportForOtherSources_WarnsThatNothingMatched()
    {
        var (coverage, _) = Arrange();
        string other = _temp.CreateFileWithContent("Other.cs", "public class Other { public void M() { } }");
        var facade = new CognitiveAnalysisFacade(
            new SourceFileFinder(),
            new CognitiveCodeAnalyser(),
            CognitiveConfigurationDefaults.Create(),
            new ScoreCalculator(),
            new CoberturaReader(),
            new ClassCouplingAnalyser());
        var metrics = facade.AnalyseSourceFiles([other]);

        var result = facade.LoadCoverageData(coverage, metrics);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.Success, Is.True);
            Assert.That(result.WarningMessage, Does.StartWith("Loaded 3 coverage entries").And.Contain("none matched"));
            Assert.That(metrics.Single().HasCoverageData, Is.False);
        }
    }
}
