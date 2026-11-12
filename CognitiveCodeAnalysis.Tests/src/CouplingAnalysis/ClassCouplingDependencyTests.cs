/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CouplingAnalysis;

namespace CognitiveCodeAnalysis.Tests.CouplingAnalysis;

public class ClassCouplingDependencyTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private Dictionary<string, ClassCouplingMetrics> Analyse(params string[] sources)
    {
        var files = sources.Select((s, i) => _temp.CreateFileWithContent($"F{i}.cs", s)).ToList();
        return new ClassCouplingAnalyser().Analyse(files).ToDictionary(m => m.ClassName);
    }

    private static void AssertCoupling(Dictionary<string, ClassCouplingMetrics> all, string name, int incoming, int outgoing)
    {
        Assert.That(all, Contains.Key(name));
        using (Assert.EnterMultipleScope())
        {
            Assert.That(all[name].IncomingCoupling, Is.EqualTo(incoming), name + " incoming");
            Assert.That(all[name].OutgoingCoupling, Is.EqualTo(outgoing), name + " outgoing");
        }
    }

    [Test]
    public void BaseClassAndInterfaces_CountAsOutgoingDependencies()
    {
        var all = Analyse("""
            namespace N;
            public interface IThing { }
            public interface IOther { }
            public class Base { }
            public class Derived : Base, IThing, IOther { }
            """);

        AssertCoupling(all, "N.Derived", incoming: 0, outgoing: 3);
        AssertCoupling(all, "N.Base", incoming: 1, outgoing: 0);
        AssertCoupling(all, "N.IThing", incoming: 1, outgoing: 0);
        AssertCoupling(all, "N.IOther", incoming: 1, outgoing: 0);
        Assert.That(all["N.Base"].Stability, Is.EqualTo(1.0));
        Assert.That(all["N.Derived"].Stability, Is.EqualTo(0.0));
    }

    [Test]
    public void SystemObjectBaseAndFrameworkTypes_AreNotDependencies()
    {
        var all = Analyse("""
            using System.Collections.Generic;
            namespace N;
            public class Plain { public List<string> Items = new(); public string S = ""; }
            """);

        AssertCoupling(all, "N.Plain", incoming: 0, outgoing: 0);
    }

    [Test]
    public void StructsInterfacesAndNestedTypes_AreAnalysedWithQualifiedKeys()
    {
        var all = Analyse("""
            namespace N;
            public struct Point { public int X; }
            public interface IShape { Point Origin { get; } }
            public class Outer
            {
                public class Inner { public Point P; }
            }
            """);

        Assert.That(all.Keys, Is.EquivalentTo(new[] { "N.Point", "N.IShape", "N.Outer", "N.Outer.Inner" }));
        AssertCoupling(all, "N.Point", incoming: 3, outgoing: 0);
        AssertCoupling(all, "N.IShape", incoming: 0, outgoing: 1);
        AssertCoupling(all, "N.Outer.Inner", incoming: 0, outgoing: 1);
        // The outer type lexically contains Inner, so references inside Inner are attributed to both.
        AssertCoupling(all, "N.Outer", incoming: 0, outgoing: 1);
    }

    [Test]
    public void GlobalNamespaceTypes_AreKeyedWithRoslynGlobalNamespaceDisplayName()
    {
        // Known defect (tracked for the bug-fix PR): the key should be the bare type name, but Roslyn renders the
        // global namespace as "<global namespace>", so the empty-namespace branch of GetTypeKey is never taken.
        var all = Analyse("""
            public class Alpha { public Beta B; }
            public class Beta { }
            """);

        AssertCoupling(all, "<global namespace>.Alpha", incoming: 0, outgoing: 1);
        AssertCoupling(all, "<global namespace>.Beta", incoming: 1, outgoing: 0);
    }

    [Test]
    public void GenericUsage_CountsTypeArgumentsAndGenericDefinitionsOnce()
    {
        var all = Analyse("""
            using System.Collections.Generic;
            namespace N;
            public class Box<T> { }
            public class Item { }
            public class User
            {
                public Box<int> A;
                public Box<string> B;
                public List<Item> Items;
            }
            """);

        AssertCoupling(all, "N.User", incoming: 0, outgoing: 2);
        AssertCoupling(all, "N.Box", incoming: 1, outgoing: 0);
        AssertCoupling(all, "N.Item", incoming: 1, outgoing: 0);
    }

    [Test]
    public void SelfReferencesAndUnresolvedTypes_AreIgnored()
    {
        var all = Analyse("""
            namespace N;
            public class Node
            {
                public Node Next;
                public Missing Unknown;
                public Node Clone() => new Node();
            }
            """);

        AssertCoupling(all, "N.Node", incoming: 0, outgoing: 0);
    }

    [Test]
    public void RepeatedReferences_CountOncePerTargetType()
    {
        var all = Analyse("""
            namespace N;
            public class Target { }
            public class Client
            {
                public Target A; public Target B;
                public Target Make(Target t) { Target local = t; return local; }
            }
            """);

        AssertCoupling(all, "N.Client", incoming: 0, outgoing: 1);
        AssertCoupling(all, "N.Target", incoming: 1, outgoing: 0);
        Assert.That(all["N.Target"].Stability, Is.EqualTo(1.0));
    }

    [Test]
    public void DependenciesAcrossFiles_AreResolved()
    {
        var all = Analyse(
            "namespace N; public class A { public B Other; }",
            "namespace N; public class B { public A Back; }");

        AssertCoupling(all, "N.A", incoming: 1, outgoing: 1);
        AssertCoupling(all, "N.B", incoming: 1, outgoing: 1);
        Assert.That(all["N.A"].Stability, Is.EqualTo(0.5));
    }

    [Test]
    public void TypesOutsideTheAnalysedFiles_AreNotCounted()
    {
        var inScope = _temp.CreateFileWithContent("Scope.cs", "namespace N; public class A { public B Other; }");
        _temp.CreateFileWithContent("Other.cs", "namespace N; public class B { }");

        var result = new ClassCouplingAnalyser().Analyse([inScope]).ToDictionary(m => m.ClassName);

        Assert.That(result.Keys, Is.EquivalentTo(new[] { "N.A" }));
        Assert.That(result["N.A"].OutgoingCoupling, Is.EqualTo(0));
    }

    [Test]
    public void EnumsAndDelegates_AreNotAnalysedAsTypes()
    {
        var all = Analyse("""
            namespace N;
            public enum Color { Red }
            public class Painter { public Color C; }
            """);

        Assert.That(all.Keys, Is.EquivalentTo(new[] { "N.Painter" }));
        AssertCoupling(all, "N.Painter", incoming: 0, outgoing: 0);
    }

    [Test]
    public async Task AnalyseAsync_EmptyList_ReturnsEmptyWithoutCompiling()
    {
        var result = await new ClassCouplingAnalyser().AnalyseAsync([]);

        Assert.That(result, Is.Empty);
    }
}
