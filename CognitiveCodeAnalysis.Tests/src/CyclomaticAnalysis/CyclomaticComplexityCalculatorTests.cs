using CognitiveCodeAnalysis.CyclomaticAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CognitiveCodeAnalysis.Tests.CyclomaticAnalysis;

public class CyclomaticComplexityCalculatorTests
{
    [Test]
    public void Calculate_SimpleMethod_ReturnsAtLeastOne()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            class C {
              void M() { }
            }
            """);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var cc = CyclomaticComplexityCalculator.calculate(method);
        Assert.That(cc, Is.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void Calculate_WithIf_IncreasesComplexity()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            class C {
              void M(int x) { if (x > 0) { } else { } }
            }
            """);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
        var cc = CyclomaticComplexityCalculator.calculate(method);
        Assert.That(cc, Is.GreaterThanOrEqualTo(2));
    }
}
