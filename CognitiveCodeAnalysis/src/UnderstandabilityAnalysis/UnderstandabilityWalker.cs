/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CognitiveCodeAnalysis.UnderstandabilityAnalysis;

/// <summary>
/// <![CDATA[
/// Measures the cognitive complexity of one method or constructor as specified in the SonarSource white paper
/// "Cognitive Complexity" by G. Ann Campbell, with the C# choices of SonarC#.
///
/// Structural increments add 1 plus the current nesting and nest their body: if, ?:, switch statement and
/// switch expression (once, not per case), for, foreach, while, do and catch. Hybrid increments add 1 without
/// a nesting penalty: else and else if; the body of an else if nests on the level of its if. Lambdas, anonymous
/// methods and local functions nest their body without an increment. Fundamental increments add 1: each run of
/// like operators in a sequence of && / || (or of the pattern combinators and / or), each goto, and recursion
/// once per method. Conditions, selectors and loop headers stay on the level of their statement; only bodies nest.
/// ]]>
/// </summary>
public sealed class UnderstandabilityWalker : CSharpSyntaxWalker
{
    private readonly string? _methodName;
    private readonly string _typeName;
    private int _nesting;
    private int _structural;
    private int _hybrid;
    private int _fundamental;
    private int _nestingPoints;
    private bool _recursive;

    private UnderstandabilityWalker(string? methodName, string typeName)
    {
        _methodName = methodName;
        _typeName = typeName;
    }

    /// <param name="unit">The method or constructor to measure.</param>
    /// <param name="typeName">The simple name of the declaring type, which a recursive call may use as receiver.</param>
    public static Understandability Measure(BaseMethodDeclarationSyntax unit, string typeName)
    {
        // Constructors cannot call themselves by name, so only methods can be recursive.
        var walker = new UnderstandabilityWalker((unit as MethodDeclarationSyntax)?.Identifier.ValueText, typeName);
        walker.Visit((unit as ConstructorDeclarationSyntax)?.Initializer);
        walker.Visit(unit.Body);
        walker.Visit(unit.ExpressionBody);

        int recursion = walker._recursive ? 1 : 0;
        return new Understandability(
            walker._structural,
            walker._hybrid,
            walker._fundamental + recursion,
            walker._nestingPoints,
            recursion);
    }

    private void Structural()
    {
        _structural++;
        _nestingPoints += _nesting;
    }

    private void Nested(SyntaxNode? node)
    {
        _nesting++;
        Visit(node);
        _nesting--;
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        Structural();
        VisitIfChain(node);
    }

    /// <summary>An else if belongs to the chain: it is no new structure, so its body is not nested deeper.</summary>
    private void VisitIfChain(IfStatementSyntax node)
    {
        Visit(node.Condition);
        Nested(node.Statement);
        if (node.Else is null)
        {
            return;
        }

        _hybrid++;
        if (node.Else.Statement is IfStatementSyntax elseIf)
        {
            VisitIfChain(elseIf);
        }
        else
        {
            Nested(node.Else.Statement);
        }
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        Structural();
        Visit(node.Condition);
        Nested(node.WhenTrue);
        Nested(node.WhenFalse);
    }

    public override void VisitSwitchStatement(SwitchStatementSyntax node)
    {
        Structural();
        Visit(node.Expression);
        foreach (SwitchSectionSyntax section in node.Sections)
        {
            Nested(section);
        }
    }

    public override void VisitSwitchExpression(SwitchExpressionSyntax node)
    {
        Structural();
        Visit(node.GoverningExpression);
        foreach (SwitchExpressionArmSyntax arm in node.Arms)
        {
            Nested(arm);
        }
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        Structural();
        Visit(node.Declaration);
        foreach (ExpressionSyntax initializer in node.Initializers)
        {
            Visit(initializer);
        }

        Visit(node.Condition);
        foreach (ExpressionSyntax incrementor in node.Incrementors)
        {
            Visit(incrementor);
        }

        Nested(node.Statement);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node) => VisitForEach(node);

    public override void VisitForEachVariableStatement(ForEachVariableStatementSyntax node) => VisitForEach(node);

    private void VisitForEach(CommonForEachStatementSyntax node)
    {
        Structural();
        Visit(node.Expression);
        Nested(node.Statement);
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        Structural();
        Visit(node.Condition);
        Nested(node.Statement);
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
        Structural();
        Nested(node.Statement);
        Visit(node.Condition);
    }

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        Structural();
        Visit(node.Filter);
        Nested(node.Block);
    }

    public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) => Nested(node.Body);

    public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) => Nested(node.Body);

    public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) => Nested(node.Body);

    public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        Nested(node.Body);
        Nested(node.ExpressionBody);
    }

    /// <summary>Every goto, including <c>goto case</c> and <c>goto default</c>, breaks the linear flow.</summary>
    public override void VisitGotoStatement(GotoStatementSyntax node) => _fundamental++;

    /// <summary>
    /// A call is recursive when it has the method's name (overloads match too) and no receiver,
    /// <c>this</c> or the simple name of the declaring type as receiver.
    /// </summary>
    public override void VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (_methodName is not null && IsCallToOwnMethod(node.Expression))
        {
            _recursive = true;
        }

        base.VisitInvocationExpression(node);
    }

    private bool IsCallToOwnMethod(ExpressionSyntax callee) => callee switch
    {
        SimpleNameSyntax name => name.Identifier.ValueText == _methodName,
        MemberAccessExpressionSyntax { Expression: var receiver, Name: var name } =>
            name.Identifier.ValueText == _methodName && IsOwnReceiver(receiver),
        _ => false,
    };

    private bool IsOwnReceiver(ExpressionSyntax receiver) => receiver switch
    {
        ThisExpressionSyntax => true,
        IdentifierNameSyntax type => type.Identifier.ValueText == _typeName,
        _ => false,
    };

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        CountSequence(node);
        base.VisitBinaryExpression(node);
    }

    public override void VisitBinaryPattern(BinaryPatternSyntax node)
    {
        CountSequence(node);
        base.VisitBinaryPattern(node);
    }

    /// <summary>Counts a whole sequence once, at its outermost operator; the nested operators belong to it.</summary>
    private void CountSequence(SyntaxNode node)
    {
        if (IsLogical(node) && !IsLogical(ParentSkippingParentheses(node)))
        {
            _fundamental += OperatorRuns(node);
        }
    }

    /// <summary>The number of runs of like operators in the sequence, such as 2 for <c>a &amp;&amp; b || c</c>.</summary>
    private static int OperatorRuns(SyntaxNode sequence)
    {
        var operators = new List<SyntaxKind>();
        Flatten(sequence, operators);

        return operators.Where((kind, i) => i == 0 || kind != operators[i - 1]).Count();
    }

    /// <summary>Collects the operators of a sequence in source order, looking through parentheses.</summary>
    private static void Flatten(SyntaxNode node, List<SyntaxKind> operators)
    {
        SyntaxNode unwrapped = SkipParentheses(node);
        if (!IsLogical(unwrapped))
        {
            return;
        }

        // Both a binary expression and a binary pattern have exactly two child nodes: the left and the right operand.
        SyntaxNode[] operands = unwrapped.ChildNodes().ToArray();
        Flatten(operands[0], operators);
        operators.Add(unwrapped.Kind());
        Flatten(operands[1], operators);
    }

    private static SyntaxNode SkipParentheses(SyntaxNode node) => node switch
    {
        ParenthesizedExpressionSyntax parenthesized => SkipParentheses(parenthesized.Expression),
        ParenthesizedPatternSyntax parenthesized => SkipParentheses(parenthesized.Pattern),
        _ => node,
    };

    private static SyntaxNode? ParentSkippingParentheses(SyntaxNode node)
    {
        SyntaxNode? parent = node.Parent;
        return parent is ParenthesizedExpressionSyntax or ParenthesizedPatternSyntax
            ? ParentSkippingParentheses(parent)
            : parent;
    }

    private static bool IsLogical(SyntaxNode? node) =>
        node.IsKind(SyntaxKind.LogicalAndExpression)
        || node.IsKind(SyntaxKind.LogicalOrExpression)
        || node.IsKind(SyntaxKind.AndPattern)
        || node.IsKind(SyntaxKind.OrPattern);
}
