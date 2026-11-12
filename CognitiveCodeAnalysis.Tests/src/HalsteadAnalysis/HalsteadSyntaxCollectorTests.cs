/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.HalsteadAnalysis;

using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CognitiveCodeAnalysis.Tests.HalsteadAnalysis;

public class HalsteadSyntaxCollectorTests
{
    private static HalsteadMetrics Collect(string methodSource)
    {
        var tree = CSharpSyntaxTree.ParseText($"class C {{ {methodSource} }}");
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        return HalsteadSyntaxCollector.CollectForMethod(method, "C::M");
    }

    [Test]
    public void Body_CountsBinaryAssignmentAndInvocationOperators_AndIdentifierLiteralOperands()
    {
        // operators: Assignment(Simple), Binary(Add), Invocation, Assignment(Add)  -> total 4, distinct 4
        // operands: x 1 y | Foo x | x 2  -> total 7, distinct {x, 1, y, Foo, 2} = 5
        var m = Collect("void M(int y) { int x; x = 1 + y; Foo(x); x += 2; }");

        using (Assert.EnterMultipleScope())
        {
            Assert.That(m.TotalOperators, Is.EqualTo(4));
            Assert.That(m.DistinctOperators, Is.EqualTo(4));
            Assert.That(m.TotalOperands, Is.EqualTo(7));
            Assert.That(m.DistinctOperands, Is.EqualTo(5));
            Assert.That(m.Identifier, Is.EqualTo("C::M"));
        }
    }

    [Test]
    public void RepeatedOperators_AreDistinctByKind()
    {
        var m = Collect("void M(int a) { a = a + 1; a = a + 2; a = a * 3; }");

        Assert.That(m.TotalOperators, Is.EqualTo(6));
        Assert.That(m.DistinctOperators, Is.EqualTo(3));
    }

    [Test]
    public void ExpressionBody_IsAnalysed()
    {
        var m = Collect("int M(int a) => a + 1;");

        Assert.That(m.TotalOperators, Is.EqualTo(1));
        Assert.That(m.TotalOperands, Is.EqualTo(2));
    }

    [Test]
    public void BodyWins_OverNothing_AndAbstractMethodsYieldEmptyMetrics()
    {
        var tree = CSharpSyntaxTree.ParseText("abstract class C { public abstract void M(); }");
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();

        var m = HalsteadSyntaxCollector.CollectForMethod(method, "C::M");

        Assert.That(m.ProgramLength, Is.EqualTo(0));
        Assert.That(m.Volume, Is.EqualTo(0));
        Assert.That(m.Identifier, Is.EqualTo("C::M"));
    }

    [Test]
    public void UnaryAndOtherConstructs_AreNotCounted()
    {
        var m = Collect("bool M(bool a) { return !a; }");

        Assert.That(m.TotalOperators, Is.EqualTo(0));
        Assert.That(m.TotalOperands, Is.EqualTo(1));
    }
}
