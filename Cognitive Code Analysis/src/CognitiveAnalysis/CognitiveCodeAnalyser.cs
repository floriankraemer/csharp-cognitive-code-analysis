using CognitiveCodeAnalysis.Configuration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class CyclomaticComplexityCalculator
{
    /// <summary>
    /// <![CDATA[
    /// Calculates the cyclomatic complexity of a method.
    /// Cyclomatic complexity is a quantitative measure of the number of linearly independent paths through a program's source code.
    /// Formula: M = number of decision points + 1
    /// Reference: https://en.wikipedia.org/wiki/Cyclomatic_complexity
    /// ]]>
    /// </summary>
    /// <param name="methodNode">The method declaration to analyse</param>
    /// <returns>The cyclomatic complexity value (minimum is 1 for a method with no decision points)</returns>
    public static int CalculateCyclomaticComplexity(MethodDeclarationSyntax methodNode)
    {
        // Methods with no body have complexity 1 (single path)
        if (methodNode.Body == null && methodNode.ExpressionBody == null)
        {
            return 1;
        }

        // Get all descendant nodes from either body type
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

public class CognitiveCodeAnalyser
{
    /// <summary>
    /// <![CDATA[
    /// Takes a list of C# source code files and analyses them to extract cognitive metrics for each method.
    /// ]]>
    /// </summary>
    /// <param name="files"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public CognitiveMetricsCollection AnalyseFiles(
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

    private static CognitiveMetricsCollection AnalyseClasses(
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

    private static CognitiveMetricsCollection ExtractMetricsFromClasses(
        CognitiveConfiguration configuration,
        ClassDeclarationSyntax classNode,
        CognitiveMetricsCollection metricsCollection,
        string file
    ) {
        string fullClassName = GetFullyQualifiedClassName(classNode);

        // FindSourceFiles all methods in the class
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
                methodSignature: GetFullSignature(methodNode),
                methodLineNumber: methodNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                ifCount: ifCount,
                argumentCount: methodNode.ParameterList.Parameters.Count,
                linesOfCode: GetLinesOfCode(methodNode),
                elseCount: elseCount,
                tryCatchCount: tryCount,
                returnCount: methodNode.DescendantNodes().OfType<ReturnStatementSyntax>().Count(),
                nestingLevels: CalculateNestingLevels(methodNode, configuration)
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
    /// <![CDATA[
    /// Combine namespace, containing types, and class name
    /// ]]>
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
        if (!configuration.CountElseAsNesting)
        {
            return currentDepth;
        }

        int depthForElse = currentDepth + 1;
        maxDepth = Math.Max(maxDepth, depthForElse);

        return depthForElse;
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
    /// <![CDATA[
    /// Control flow structures that create nesting levels.
    /// Exclude ElseClauseSyntax and IfStatementSyntax as they're handled separately.
    /// ]]>
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
    /// <![CDATA[
    /// Calculates and sets the total score for the given metrics.
    /// ]]>
    /// </summary>
    /// <param name="metrics">The cognitive metrics to calculate the total score for</param>
    public static void CalculateTotalScore(CognitiveMetrics metrics)
    {
        metrics.totalScore = metrics.ifScore + metrics.elseScore + metrics.loopScore + metrics.switchScore +
                             metrics.tryCatchScore + metrics.returnScore + metrics.argumentScore + metrics.nestingScore;
    }
}
