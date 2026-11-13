/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CodeCoverage;

/// <summary>
/// The Cobertura reader supports reports with counts (lines-valid, branches-valid, ...) and reports with rates only
/// (line-rate, branch-rate), which is all that coverlet writes.
/// </summary>
public class CoberturaRatesTests
{
    private TempFiles _temp = null!;
    private CultureInfo _culture = null!;

    [SetUp]
    public void SetUp()
    {
        _temp = new TempFiles();
        _culture = CultureInfo.CurrentCulture;
    }

    [TearDown]
    public void TearDown()
    {
        CultureInfo.CurrentCulture = _culture;
        _temp.CleanUp();
    }

    private Coverage Method(string methodAttributes, string methodBody = "")
    {
        string xml = $"""
            <coverage><packages><package name="P"><classes><class name="C" filename="C.cs"><methods>
              <method name="M" {methodAttributes}>{methodBody}</method>
            </methods></class></classes></package></packages></coverage>
            """;
        return new CoberturaReader().ReadCoverage(_temp.CreateFileWithContent("c.xml", xml)).Single(c => c.IsMethodLevel);
    }

    [Test]
    public void Rates_AreUsedWhenCountsAreAbsent()
    {
        var c = Method("""line-rate="0.75" branch-rate="0.5" """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(c.LinesTotal, Is.EqualTo(0));
            Assert.That(c.LineCoveragePercentage, Is.EqualTo(75.0));
            Assert.That(c.BranchCoveragePercentage, Is.EqualTo(50.0));
        }
    }

    [Test]
    public void Counts_WinOverRatesWhenBothArePresent()
    {
        var c = Method("""lines-covered="1" lines-valid="4" branches-covered="1" branches-valid="4" line-rate="0.9" branch-rate="0.9" """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(c.LineCoveragePercentage, Is.EqualTo(25.0));
            Assert.That(c.BranchCoveragePercentage, Is.EqualTo(25.0));
        }
    }

    [Test]
    public void CountsMode_WithZeroValidBranches_HasNoBranchData_EvenIfARateIsPresent()
    {
        var c = Method("""lines-covered="2" lines-valid="2" branches-covered="0" branches-valid="0" line-rate="1" branch-rate="1" """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(c.LineCoveragePercentage, Is.EqualTo(100.0));
            Assert.That(c.BranchCoveragePercentage, Is.Null);
        }
    }

    [Test]
    public void LinesAreCountedFromCountsWhileBranchesComeFromTheRate_WhenOnlyOneKindIsPresent()
    {
        var c = Method("""lines-covered="1" lines-valid="2" branch-rate="0.25" """);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(c.LineCoveragePercentage, Is.EqualTo(50.0));
            Assert.That(c.BranchCoveragePercentage, Is.EqualTo(25.0));
        }
    }

    [Test]
    public void RateMode_MethodWhoseLinesHaveNoBranches_HasNoBranchDataDespiteBranchRateOfOne()
    {
        var c = Method(
            """line-rate="1" branch-rate="1" """,
            """<lines><line number="38" hits="7" branch="False"/><line number="40" hits="7" branch="False"/></lines>""");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(c.LineCoveragePercentage, Is.EqualTo(100.0));
            Assert.That(c.BranchCoveragePercentage, Is.Null);
        }
    }

    [Test]
    public void RateMode_MethodWithABranchLine_KeepsItsBranchRate()
    {
        var c = Method(
            """line-rate="1" branch-rate="0.5" """,
            """<lines><line number="52" hits="7" branch="True" condition-coverage="50% (1/2)"/><line number="54" hits="7" branch="False"/></lines>""");

        Assert.That(c.BranchCoveragePercentage, Is.EqualTo(50.0));
    }

    [Test]
    public void RateMode_BranchFlagIsCaseInsensitive()
    {
        var c = Method("""line-rate="1" branch-rate="0.5" """, """<lines><line number="1" hits="1" branch="true"/></lines>""");

        Assert.That(c.BranchCoveragePercentage, Is.EqualTo(50.0));
    }

    [Test]
    public void RateMode_WithoutALinesList_TrustsTheBranchRate()
    {
        var c = Method("""line-rate="1" branch-rate="0.5" """);

        Assert.That(c.BranchCoveragePercentage, Is.EqualTo(50.0));
    }

    [Test]
    public void RateMode_WithoutAnyRate_IsZeroLineCoverageAndNoBranchData()
    {
        var c = Method("");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(c.LineCoveragePercentage, Is.EqualTo(0.0));
            Assert.That(c.BranchCoveragePercentage, Is.Null);
        }
    }

    [TestCase("abc", 0.0)]
    [TestCase("", 0.0)]
    [TestCase("1.5", 100.0)]
    [TestCase("-0.5", 0.0)]
    [TestCase("0", 0.0)]
    [TestCase("1", 100.0)]
    public void RateMode_MalformedOrOutOfRangeRates_AreClamped(string rate, double expectedLinePercent)
    {
        var c = Method($"""line-rate="{rate}" """);

        Assert.That(c.LineCoveragePercentage, Is.EqualTo(expectedLinePercent));
    }

    [Test]
    public void RateMode_ZeroBranchRate_IsAnExistingValueNotMissingData()
    {
        var c = Method("""line-rate="0" branch-rate="0" """, """<lines><line number="1" hits="0" branch="True"/></lines>""");

        Assert.That(c.BranchCoveragePercentage, Is.EqualTo(0.0));
    }

    [Test]
    public void Rates_AreParsedWithTheInvariantCulture()
    {
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        var c = Method("""line-rate="0.75" branch-rate="0.5" """);

        Assert.That(c.LineCoveragePercentage, Is.EqualTo(75.0));
    }

    [Test]
    public void ClassLevelRates_AreReadToo()
    {
        string xml = """<coverage><packages><package name="P"><classes><class name="C" filename="C.cs" line-rate="0.4" branch-rate="0.2"/></classes></package></packages></coverage>""";

        var c = new CoberturaReader().ReadCoverage(_temp.CreateFileWithContent("c.xml", xml)).Single();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(c.LineCoveragePercentage, Is.EqualTo(40.0));
            Assert.That(c.BranchCoveragePercentage, Is.EqualTo(20.0));
        }
    }

    [Test]
    public void CoverletClassNames_AreNotPrefixedWithThePackageTwice()
    {
        string xml = """<coverage><packages><package name="Asm"><classes><class name="Asm.Ns.C" filename="C.cs"/><class name="Plain" filename="D.cs"/></classes></package></packages></coverage>""";

        var list = new CoberturaReader().ReadCoverage(_temp.CreateFileWithContent("c.xml", xml)).ToList();

        Assert.That(list.Select(c => c.FullyQualifiedClassName), Is.EqualTo(new[] { "Asm.Ns.C", "Asm.Plain" }));
    }

    [Test]
    public void ClassNameThatOnlyStartsWithThePackageNameAsPrefixText_IsStillPrefixed()
    {
        string xml = """<coverage><packages><package name="Asm"><classes><class name="AsmHelper" filename="C.cs"/></classes></package></packages></coverage>""";

        var c = new CoberturaReader().ReadCoverage(_temp.CreateFileWithContent("c.xml", xml)).Single();

        Assert.That(c.FullyQualifiedClassName, Is.EqualTo("Asm.AsmHelper"));
    }

    [Test]
    public void VsCoverage_HasNoBranchData()
    {
        string path = _temp.CreateFileWithContent("vs.xml", """<results><module><range source_id="1" start_line="1" covered="yes"/><source_file id="1" path="/src/A.cs"/></module></results>""");

        var c = new VsCoverageReader().ReadCoverage(path).Single();

        Assert.That(c.BranchCoveragePercentage, Is.Null);
    }

    [Test]
    public void Coverage_WithBranchCounts_ComputesTheBranchPercentage()
    {
        var c = new Coverage { BranchesCovered = 1, BranchesTotal = 4 };

        Assert.That(c.BranchCoveragePercentage, Is.EqualTo(25.0));
    }

    [Test]
    public void Coverage_WithoutAnyBranchInformation_HasNoBranchPercentage()
    {
        Assert.That(new Coverage().BranchCoveragePercentage, Is.Null);
        Assert.That(new Coverage().LineCoveragePercentage, Is.EqualTo(0.0));
    }
}
