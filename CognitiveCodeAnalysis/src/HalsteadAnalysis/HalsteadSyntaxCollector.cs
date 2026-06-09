/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CognitiveCodeAnalysis.HalsteadAnalysis;

/// <summary>
/// Collects operator/operand tags from a method body, mirroring <c>HalsteadMetricsVisitor.php</c>
/// (binary ops, assignments, invocations = operators; literals and simple identifiers = operands).
/// </summary>
public static class HalsteadSyntaxCollector
{
    public static HalsteadMetrics CollectForMethod(MethodDeclarationSyntax methodNode, string identifier)
    {
        var operators = new List<string>();
        var operands = new List<string>();

        SyntaxNode? root = methodNode.Body as SyntaxNode ?? methodNode.ExpressionBody;
        if (root == null)
        {
            return HalsteadMetricsCalculator.Calculate(operators, operands, identifier);
        }

        foreach (SyntaxNode node in root.DescendantNodes())
        {
            switch (node)
            {
                case BinaryExpressionSyntax binary:
                    operators.Add(nameof(BinaryExpressionSyntax) + ":" + binary.Kind());
                    break;
                case AssignmentExpressionSyntax assignment:
                    operators.Add(nameof(AssignmentExpressionSyntax) + ":" + assignment.Kind());
                    break;
                case InvocationExpressionSyntax:
                    operators.Add(nameof(InvocationExpressionSyntax));
                    break;
                case LiteralExpressionSyntax literal:
                    operands.Add(literal.ToString());
                    break;
                case IdentifierNameSyntax identifierName:
                    operands.Add(identifierName.Identifier.Text);
                    break;
            }
        }

        return HalsteadMetricsCalculator.Calculate(operators, operands, identifier);
    }
}
