using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CognitiveCodeAnalysis.CyclomaticAnalysis;
/// <summary>
/// https://en.wikipedia.org/wiki/Cyclomatic_complexity
/// </summary>
public class CyclomaticComplexityCalculator
{
    /// <summary>
    /// Methods with no body have complexity 1 (single path)
    /// </summary>
    /// <param name="methodNode"></param>
    /// <returns></returns>
    private static bool MethodHasNoBody(MethodDeclarationSyntax methodNode)
    {
        return methodNode.Body == null && methodNode.ExpressionBody == null;
    }

    /// <summary>
    /// <![CDATA[
    /// Calculates the cyclomatic complexity of a method.
    ///
    /// Cyclomatic complexity is a quantitative measure of the number of linearly independent paths through a program's source code.
    /// Formula: M = number of decision points + 1
    ///
    /// Reference: https://en.wikipedia.org/wiki/Cyclomatic_complexity
    /// ]]>
    /// </summary>
    /// <param name="methodNode">The method declaration to analyse</param>
    /// <returns>The cyclomatic complexity value (minimum is 1 for a method with no decision points)</returns>
    public static int calculate(MethodDeclarationSyntax methodNode)
    {
        if (MethodHasNoBody(methodNode))
        {
            return 1;
        }

        IEnumerable<SyntaxNode> allNodes = methodNode.Body != null
            ? methodNode.Body.DescendantNodes()
            : methodNode.ExpressionBody!.DescendantNodes();

        int decisionPoints = 0;

        // Count if statements (each if is a decision point)
        decisionPoints += allNodes.OfType<IfStatementSyntax>().Count();

        // Count loops (while, for, foreach, do-while)
        decisionPoints += allNodes.OfType<WhileStatementSyntax>().Count();
        decisionPoints += allNodes.OfType<ForStatementSyntax>().Count();
        decisionPoints += allNodes.OfType<ForEachStatementSyntax>().Count();
        decisionPoints += allNodes.OfType<DoStatementSyntax>().Count();

        // Count switch cases (each case section is a decision point)
        var switchStatements = allNodes.OfType<SwitchStatementSyntax>().ToList();
        foreach (SwitchStatementSyntax switchStatement in switchStatements)
        {
            decisionPoints += switchStatement.Sections.Count;
        }

        // Count catch clauses (each catch is a decision point)
        decisionPoints += allNodes.OfType<CatchClauseSyntax>().Count();

        // Count conditional operators (ternary ? :)
        decisionPoints += allNodes.OfType<ConditionalExpressionSyntax>().Count();

        // Count logical operators in conditions (&&, ||)
        // These create additional decision points within if/while conditions
        decisionPoints += allNodes.OfType<BinaryExpressionSyntax>()
            .Count(e => e.OperatorToken.IsKind(SyntaxKind.AmpersandAmpersandToken) ||
                        e.OperatorToken.IsKind(SyntaxKind.BarBarToken));

        // Cyclomatic complexity = decision points + 1
        return decisionPoints + 1;
    }
}
