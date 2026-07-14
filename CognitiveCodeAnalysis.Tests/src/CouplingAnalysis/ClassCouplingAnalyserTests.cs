/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Common;
using CognitiveCodeAnalysis.CouplingAnalysis;
using CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.CouplingAnalysis;

public class ClassCouplingAnalyserTests
{
    private TempFiles _tempFiles = null!;
    private ClassCouplingAnalyser _analyser = null!;

    [SetUp]
    public void SetUp()
    {
        _tempFiles = new TempFiles();
        _analyser = new ClassCouplingAnalyser();
    }

    [TearDown]
    public void TearDown()
    {
        _tempFiles.CleanUp();
    }

    [Test]
    public void Analyse_ChainOfDependencies_ComputesIncomingOutgoingAndStability()
    {
        _tempFiles.CreateFileWithContent(
            "A.cs",
            """
            namespace Chain;
            public class A
            {
                private readonly B _b = new B();
            }
            """
        );
        _tempFiles.CreateFileWithContent(
            "B.cs",
            """
            namespace Chain;
            public class B
            {
                private readonly C _c = new C();
            }
            """
        );
        _tempFiles.CreateFileWithContent(
            "C.cs",
            """
            namespace Chain;
            public class C
            {
                public int Value;
            }
            """
        );

        var files = Directory.GetFiles(_tempFiles.tmpDirectory, "*.cs").OrderBy(f => f).ToList();
        var metrics = _analyser.Analyse(files).ToDictionary(m => m.ClassName);

        Assert.That(metrics["Chain.A"].OutgoingCoupling, Is.EqualTo(1));
        Assert.That(metrics["Chain.A"].IncomingCoupling, Is.EqualTo(0));
        Assert.That(metrics["Chain.A"].Stability, Is.EqualTo(0.0));

        Assert.That(metrics["Chain.B"].OutgoingCoupling, Is.EqualTo(1));
        Assert.That(metrics["Chain.B"].IncomingCoupling, Is.EqualTo(1));
        Assert.That(metrics["Chain.B"].Stability, Is.EqualTo(0.5).Within(0.001));

        Assert.That(metrics["Chain.C"].OutgoingCoupling, Is.EqualTo(0));
        Assert.That(metrics["Chain.C"].IncomingCoupling, Is.EqualTo(1));
        Assert.That(metrics["Chain.C"].Stability, Is.EqualTo(1.0));
    }

    [Test]
    public void Analyse_NoCrossTypeReferences_HasZeroCouplingAndStability()
    {
        _tempFiles.CreateFileWithContent(
            "Isolated.cs",
            """
            namespace Solo;
            public class One
            {
                public int Add(int a, int b) => a + b;
            }
            """
        );

        var file = Directory.GetFiles(_tempFiles.tmpDirectory, "*.cs").Single();
        var metric = _analyser.Analyse([file]).Single();

        Assert.That(metric.ClassName, Is.EqualTo("Solo.One"));
        Assert.That(metric.IncomingCoupling, Is.EqualTo(0));
        Assert.That(metric.OutgoingCoupling, Is.EqualTo(0));
        Assert.That(metric.Stability, Is.EqualTo(0.0));
    }

    [Test]
    public void Analyse_PartialClassAcrossFiles_UsesSameCouplingForFqcn()
    {
        _tempFiles.CreateFileWithContent(
            "Partial1.cs",
            """
            namespace Parts;
            public partial class Widget
            {
                private readonly Helper _helper = new Helper();
            }
            """
        );
        _tempFiles.CreateFileWithContent(
            "Partial2.cs",
            """
            namespace Parts;
            public partial class Widget
            {
                public int Id;
            }
            """
        );
        _tempFiles.CreateFileWithContent(
            "Helper.cs",
            """
            namespace Parts;
            public class Helper
            {
                public string Name = "x";
            }
            """
        );

        var files = Directory.GetFiles(_tempFiles.tmpDirectory, "*.cs").ToList();
        var metrics = _analyser.Analyse(files).ToDictionary(m => m.ClassName);

        Assert.That(metrics["Parts.Widget"].OutgoingCoupling, Is.EqualTo(1));
        Assert.That(metrics["Parts.Widget"].IncomingCoupling, Is.EqualTo(0));
        Assert.That(metrics["Parts.Helper"].IncomingCoupling, Is.EqualTo(1));
    }

    [Test]
    public void Analyse_EmptyFileList_ReturnsEmpty()
    {
        Assert.That(_analyser.Analyse([]), Is.Empty);
    }

    [Test]
    public async Task AnalyseCompiled_WithProgress_ReportsCouplingPhases()
    {
        _tempFiles.CreateFileWithContent(
            "A.cs",
            """
            namespace Chain;
            public class A
            {
                private readonly B _b = new B();
            }
            """
        );
        _tempFiles.CreateFileWithContent(
            "B.cs",
            """
            namespace Chain;
            public class B
            {
                public int Value;
            }
            """
        );

        var files = Directory.GetFiles(_tempFiles.tmpDirectory, "*.cs").OrderBy(f => f).ToList();
        CompiledSourceSet sources = await CompiledSourceSet.BuildAsync(files);
        var collector = new AnalysisProgressCollector();

        _analyser.AnalyseCompiled(sources, collector);

        var reports = collector.Reports;
        var analysingReports = reports.Where(r => r.Phase == AnalysisProgressPhase.AnalysingCoupling).ToList();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(analysingReports, Is.Not.Empty);
            Assert.That(analysingReports[0].ProcessedFiles, Is.EqualTo(0));
            Assert.That(analysingReports[0].TotalFiles, Is.EqualTo(2));
            Assert.That(analysingReports.Select(r => r.ProcessedFiles), Does.Contain(2));
            Assert.That(reports[^1].Phase, Is.EqualTo(AnalysisProgressPhase.CouplingCompleted));
            Assert.That(reports[^1].TotalFiles, Is.EqualTo(2));
            Assert.That(reports[^1].ProcessedFiles, Is.EqualTo(2));
        }
    }

    [Test]
    public async Task AnalyseCompiled_ManyTypes_ProducesSameResultsAsSequentialBaseline()
    {
        for (int i = 0; i < 20; i++)
        {
            int dependency = (i + 1) % 20;
            _tempFiles.CreateFileWithContent(
                $"Class{i}.cs",
                $$"""
                namespace Many;
                public class Class{{i}}
                {
                    private Class{{dependency}}? _next;
                    public void Link(Class{{dependency}} next) => _next = next;
                }
                """
            );
        }

        var files = Directory.GetFiles(_tempFiles.tmpDirectory, "*.cs").OrderBy(f => f).ToList();
        CompiledSourceSet sources = await CompiledSourceSet.BuildAsync(files);

        var metrics = _analyser.AnalyseCompiled(sources).ToDictionary(m => m.ClassName);

        Assert.That(metrics, Has.Count.EqualTo(20));
        foreach (var metric in metrics.Values)
        {
            Assert.That(metric.OutgoingCoupling, Is.EqualTo(1), $"{metric.ClassName} outgoing");
            Assert.That(metric.IncomingCoupling, Is.EqualTo(1), $"{metric.ClassName} incoming");
        }
    }
}
