using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

/// <summary>
/// Detects whether a method is pure (has no side effects).
/// A pure method always returns the same output for the same input and doesn't modify state.
/// </summary>
public static class PureMethodDetector
{
    /// <summary>
    /// Determines if a method is pure (has no side effects).
    /// A pure method always returns the same output for the same input and doesn't modify state.
    /// </summary>
    /// <param name="methodNode">The method declaration to analyse</param>
    /// <returns>True if the method appears to be pure, false otherwise</returns>
    public static bool IsPure(MethodDeclarationSyntax methodNode)
    {
        // Methods with no body (abstract, extern, etc.) cannot be analysed
        if (methodNode.Body == null && methodNode.ExpressionBody == null)
        {
            return false;
        }

        // Get all descendant nodes from either body type
        IEnumerable<SyntaxNode> allNodes = methodNode.Body != null
            ? methodNode.Body.DescendantNodes()
            : methodNode.ExpressionBody!.DescendantNodes();

        // Check for assignments (field/property modifications)
        if (allNodes.OfType<AssignmentExpressionSyntax>().Any())
        {
            return false;
        }

        if (!IsIncrementDecrementOperation(allNodes))
        {
            return false;
        }

        // Check for throw statements (exceptions are considered side effects)
        if (allNodes.OfType<ThrowStatementSyntax>().Any() || allNodes.OfType<ThrowExpressionSyntax>().Any())
        {
            return false;
        }

        // Check for method invocations that might have side effects
        // We'll be conservative and flag any method call as potentially impure
        // unless it's a known pure operation (like string operations, math operations)
        IEnumerable<InvocationExpressionSyntax> invocations = allNodes.OfType<InvocationExpressionSyntax>();
        foreach (InvocationExpressionSyntax invocation in invocations)
        {
            if (!IsKnownPureMethod(invocation))
            {
                return false;
            }
        }

        // Check for await expressions (async operations often have side effects)
        if (allNodes.OfType<AwaitExpressionSyntax>().Any())
        {
            return false;
        }

        // Check for lock statements (mutations)
        if (allNodes.OfType<LockStatementSyntax>().Any())
        {
            return false;
        }

        // If none of the above side effects are found, consider the method pure
        return true;
    }

    /// <summary>
    /// Check for increment/decrement operations (++ and --)
    /// </summary>
    /// <param name="allNodes"></param>
    /// <returns>True if no increment/decrement operations are found, false otherwise</returns>
    private static bool IsIncrementDecrementOperation(IEnumerable<SyntaxNode> allNodes)
    {
        if (allNodes.OfType<PostfixUnaryExpressionSyntax>()
            .Any(e => e.Kind() == SyntaxKind.PostIncrementExpression || e.Kind() == SyntaxKind.PostDecrementExpression))
        {
            return false;
        }

        if (allNodes.OfType<PrefixUnaryExpressionSyntax>()
            .Any(e => e.Kind() == SyntaxKind.PreIncrementExpression || e.Kind() == SyntaxKind.PreDecrementExpression))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a method invocation is known to be pure (no side effects).
    /// This is a conservative check - only methods that are definitely pure are allowed.
    /// </summary>
    /// <param name="invocation">The method invocation to check</param>
    /// <returns>True if the method is known to be pure, false otherwise</returns>
    private static bool IsKnownPureMethod(InvocationExpressionSyntax invocation)
    {
        // Get the method name being called
        string? methodName = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null
        };

        if (string.IsNullOrEmpty(methodName))
        {
            return false;
        }

        // Get the containing type/namespace if available
        string? containingType = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Expression.ToString(),
            _ => null
        };

        // Known pure methods from common types
        // String methods (most are pure)
        if (containingType == "string" || containingType?.EndsWith(".string") == true)
        {
            string[] pureStringMethods = { "ToString", "Substring", "Trim", "TrimStart", "TrimEnd",
                "ToUpper", "ToLower", "Replace", "Split", "Contains", "StartsWith", "EndsWith",
                "IndexOf", "LastIndexOf", "Compare", "CompareTo", "Equals", "GetHashCode",
                "IsNullOrEmpty", "IsNullOrWhiteSpace", "Format", "Join", "Concat" };

            if (pureStringMethods.Contains(methodName, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Math methods (typically pure)
        if (containingType == "Math" || containingType?.EndsWith(".Math") == true)
        {
            return true;
        }

        // System.Convert methods (pure transformations)
        if (containingType == "Convert" || containingType?.EndsWith(".Convert") == true)
        {
            return true;
        }

        // System.Linq methods (most are pure, but we'll be conservative)
        // Note: Some LINQ methods like ToList() have side effects, but the query itself is pure
        if (containingType?.Contains("System.Linq") == true)
        {
            // Most LINQ query methods are pure
            string[] pureLinqMethods = { "Where", "Select", "SelectMany", "OrderBy", "OrderByDescending",
                "ThenBy", "ThenByDescending", "GroupBy", "Join", "GroupJoin", "Distinct", "Union",
                "Intersect", "Except", "Skip", "Take", "First", "FirstOrDefault", "Last", "LastOrDefault",
                "Single", "SingleOrDefault", "Any", "All", "Count", "Sum", "Min", "Max", "Average",
                "Aggregate", "Reverse", "Concat", "Zip", "DefaultIfEmpty", "OfType", "Cast" };

            if (pureLinqMethods.Contains(methodName, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // If we can't determine it's pure, assume it's not
        return false;
    }
}

