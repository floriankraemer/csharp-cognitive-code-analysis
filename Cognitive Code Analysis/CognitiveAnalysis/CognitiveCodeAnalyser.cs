using CognitiveCodeAnalysis.Configuration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class CognitiveCodeAnalyser
{
    /// <summary>
    /// Takes a list of C# source code files and analyzes them to extract cognitive metrics for each method.
    /// </summary>
    /// <param name="files"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public CognitiveMetricsCollection AnalyzeFiles(
        List<string> files,
        CognitiveConfiguration configuration
    ) {
        CognitiveMetricsCollection metricsCollection = [];

        foreach (string file in files)
        {
            string fileContent = File.ReadAllText(file);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(fileContent);
            SyntaxNode root = tree.GetRoot();

            metricsCollection = AnalyseClasses(configuration, root, metricsCollection, file);
        }

        return metricsCollection;
    }

    private CognitiveMetricsCollection AnalyseClasses(
        CognitiveConfiguration configuration,
        SyntaxNode root,
        CognitiveMetricsCollection metricsCollection, string file
    ) {
        IEnumerable<ClassDeclarationSyntax> classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (ClassDeclarationSyntax classNode in classNodes)
        {
            metricsCollection = ExtractMetricsFromClasses(configuration, classNode, metricsCollection, file);
        }

        return metricsCollection;
    }

    private CognitiveMetricsCollection ExtractMetricsFromClasses(
        CognitiveConfiguration configuration,
        ClassDeclarationSyntax classNode,
        CognitiveMetricsCollection metricsCollection,
        string file
    ) {
        string fullClassName = GetFullyQualifiedClassName(classNode);

        // Find all methods in the class
        IEnumerable<MethodDeclarationSyntax> methodNodes = classNode.Members.OfType<MethodDeclarationSyntax>();
        foreach (MethodDeclarationSyntax methodNode in methodNodes)
        {
            int ifCount = methodNode.DescendantNodes()
                .OfType<IfStatementSyntax>()
                .Count();

            int elseCount = methodNode.DescendantNodes()
                .OfType<ElseClauseSyntax>()
                .Count();

            int tryCount = methodNode.DescendantNodes()
                .OfType<TryStatementSyntax>()
                .Count();

            metricsCollection.Add(new CognitiveMetrics(
                methodName: methodNode.Identifier.Text,
                className: fullClassName,
                filePath: file,
                signature: GetFullSignature(methodNode),
                methodLineNumber: methodNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                ifCount: ifCount,
                argumentCount: methodNode.ParameterList.Parameters.Count,
                linesOfCode: GetLinesOfCode(methodNode),
                elseCount: elseCount,
                tryCatchCount: tryCount,
                returnCount: methodNode.DescendantNodes().OfType<ReturnStatementSyntax>().Count(),
                nestingLevels: CalculateNestingLevels(methodNode, configuration),
                isPure: IsPureMethod(methodNode)
            ));
        }

        return metricsCollection;
    }

    private static string GetFullSignature(MethodDeclarationSyntax methodNode)
    {
        return methodNode.Modifiers + " " +
                           methodNode.ReturnType + " " +
                           methodNode.Identifier.Text +
                           methodNode.ParameterList;
    }

    private static int GetLinesOfCode(MethodDeclarationSyntax methodNode)
    {
        return methodNode.GetLocation()
                .GetLineSpan()
                .EndLinePosition.Line
            - methodNode.GetLocation()
                .GetLineSpan()
                .StartLinePosition.Line + 1;
    }

    /// <summary>
    /// Combine namespace, containing types, and class name
    /// </summary>
    /// <param name="classNode"></param>
    /// <returns></returns>
    private static string GetFullyQualifiedClassName(ClassDeclarationSyntax classNode)
    {
        IEnumerable<string> containingTypes = CollectNestedClasses(classNode);
        IEnumerable<string> namespaceParts = CollectNamespaceParts(classNode);

        return string.Join(".", namespaceParts
            .Concat(containingTypes)
            .Append(classNode.Identifier.Text)
            .Where(p => !string.IsNullOrEmpty(p))
        );
    }

    private static IEnumerable<string> CollectNestedClasses(ClassDeclarationSyntax classNode)
    {
        return classNode.Ancestors()
            .OfType<ClassDeclarationSyntax>()
            .Reverse()
            .Select(c => c.Identifier.Text);
    }

    // Collect namespace parts (including file-scoped namespaces)
    private static IEnumerable<string> CollectNamespaceParts(ClassDeclarationSyntax classNode)
    {
        return classNode.Ancestors()
            .OfType<NamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .Concat(classNode.Ancestors()
                .OfType<FileScopedNamespaceDeclarationSyntax>()
                .Select(ns => ns.Name.ToString())
            )
            .Reverse();
    }

    private static int CalculateNestingLevels(MethodDeclarationSyntax methodNode, CognitiveConfiguration configuration)
    {
        BlockSyntax? body = methodNode.Body;

        // Expression-bodied methods have no body block
        if (body == null)
        {
            return 0;
        }

        int maxDepth = 0;
        CalculateNestingDepth(body, 0, ref maxDepth, null, configuration);

        return maxDepth;
    }

    private static void CalculateNestingDepth(
        SyntaxNode node,
        int currentDepth,
        ref int maxDepth,
        SyntaxNode? parent,
        CognitiveConfiguration configuration
    ) {
        switch (node)
        {
            case BlockSyntax block:
                ProcessBlockSyntax(block, currentDepth, ref maxDepth, configuration);
                return;

            case ElseClauseSyntax elseClause:
                ProcessElseClause(elseClause, currentDepth, ref maxDepth, configuration);
                return;

            case IfStatementSyntax ifStatement:
                ProcessIfStatement(ifStatement, currentDepth, ref maxDepth, parent, configuration);
                return;
        }

        if (IsNestingNode(node))
        {
            ProcessNestingNode(currentDepth, ref maxDepth);
        }

        ProcessChildNodes(node, currentDepth, ref maxDepth, configuration);
    }

    private static void ProcessBlockSyntax(
        BlockSyntax block,
        int currentDepth,
        ref int maxDepth,
        CognitiveConfiguration configuration
    ) {
        // BlockSyntax nodes don't increase nesting depth, just process children
        foreach (SyntaxNode child in block.ChildNodes())
        {
            CalculateNestingDepth(child, currentDepth, ref maxDepth, block, configuration);
        }
    }

    private static void ProcessElseClause(ElseClauseSyntax elseClause, int currentDepth, ref int maxDepth, CognitiveConfiguration configuration)
    {
        int depthForElse = CalculateElseDepth(currentDepth, ref maxDepth, configuration);

        foreach (SyntaxNode child in elseClause.ChildNodes())
        {
            int childDepth = GetChildDepthForElse(child, currentDepth, depthForElse, configuration);
            CalculateNestingDepth(child, childDepth, ref maxDepth, elseClause, configuration);
        }
    }

    private static int CalculateElseDepth(int currentDepth, ref int maxDepth, CognitiveConfiguration configuration)
    {
        if (configuration.CountElseAsNesting)
        {
            int depthForElse = currentDepth + 1;
            maxDepth = Math.Max(maxDepth, depthForElse);

            return depthForElse;
        }

        return currentDepth;
    }

    private static int GetChildDepthForElse(SyntaxNode child, int currentDepth, int depthForElse, CognitiveConfiguration configuration)
    {
        // else-if: use the same depth as the parent if statement when CountElseIfAsNesting is false
        if (child is IfStatementSyntax && !configuration.CountElseIfAsNesting)
        {
            return currentDepth;
        }

        return depthForElse;
    }

    private static void ProcessIfStatement(IfStatementSyntax ifStatement, int currentDepth, ref int maxDepth, SyntaxNode? parent, CognitiveConfiguration configuration)
    {
        bool isElseIf = parent is ElseClauseSyntax;
        int depthForIf = CalculateIfDepth(currentDepth, isElseIf, configuration);

        maxDepth = Math.Max(maxDepth, depthForIf);

        foreach (SyntaxNode child in ifStatement.ChildNodes())
        {
            CalculateNestingDepth(child, depthForIf, ref maxDepth, ifStatement, configuration);
        }
    }

    private static int CalculateIfDepth(int currentDepth, bool isElseIf, CognitiveConfiguration configuration)
    {
        if (isElseIf && !configuration.CountElseIfAsNesting)
        {
            // else-if chains are flat - use the same depth as the parent if statement
            return currentDepth;
        }
        // Normal if statement - increment depth
        return currentDepth + 1;
    }

    private static void ProcessNestingNode(int currentDepth, ref int maxDepth)
    {
        int newDepth = currentDepth + 1;
        maxDepth = Math.Max(maxDepth, newDepth);
    }

    private static void ProcessChildNodes(SyntaxNode node, int currentDepth, ref int maxDepth, CognitiveConfiguration configuration)
    {
        foreach (SyntaxNode child in node.ChildNodes())
        {
            CalculateNestingDepth(child, currentDepth, ref maxDepth, node, configuration);
        }
    }

    /// <summary>
    /// Control flow structures that create nesting levels.
    /// Exclude ElseClauseSyntax and IfStatementSyntax as they're handled separately.
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    private static bool IsNestingNode(SyntaxNode node)
    {
        return node is ForStatementSyntax
            || node is ForEachStatementSyntax
            || node is WhileStatementSyntax
            || node is DoStatementSyntax
            || node is SwitchStatementSyntax
            || node is TryStatementSyntax
            || node is CatchClauseSyntax
            || node is FinallyClauseSyntax
            || node is LockStatementSyntax
            || node is UsingStatementSyntax
            || node is FixedStatementSyntax
            || node is CheckedStatementSyntax
            || node is UnsafeStatementSyntax;
    }

    /// <summary>
    /// Determines if a method is pure (has no side effects).
    /// A pure method always returns the same output for the same input and doesn't modify state.
    /// </summary>
    /// <param name="methodNode">The method declaration to analyze</param>
    /// <returns>True if the method appears to be pure, false otherwise</returns>
    private static bool IsPureMethod(MethodDeclarationSyntax methodNode)
    {
        // Methods with no body (abstract, extern, etc.) cannot be analyzed
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
    /// <returns></returns>
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
