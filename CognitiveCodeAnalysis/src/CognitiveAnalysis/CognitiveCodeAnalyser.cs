using System.Collections.Concurrent;

using CognitiveCodeAnalysis.Configuration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CognitiveCodeAnalysis.CognitiveAnalysis;

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
        var metricsCollection = new CognitiveMetricsCollection();

        foreach (string file in files)
        {
            string fileContent = File.ReadAllText(file);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(fileContent);
            SyntaxNode root = tree.GetRoot();

            metricsCollection = AnalyseClasses(configuration, root, metricsCollection, file);
        }

        return metricsCollection;
    }

    /// <summary>
    /// <![CDATA[
    /// Takes a list of C# source code files and analyses them to extract cognitive metrics for each method.
    /// ]]>
    /// </summary>
    /// <param name="files"></param>
    /// <param name="configuration"></param>
    /// <returns></returns>
    public async Task<CognitiveMetricsCollection> AnalyseFilesAsync(
        List<string> files,
        CognitiveConfiguration configuration,
        CancellationToken cancellationToken = default
    ) {
        // Use a thread-safe bag to collect metrics from all parallel tasks
        var allMetrics = new ConcurrentBag<CognitiveMetrics>();

        var fileTasks = files.Select(async file =>
        {
#if NETSTANDARD2_1_OR_GREATER
            string fileContent = await File.ReadAllTextAsync(file, cancellationToken);
#else
            string fileContent = File.ReadAllText(file);
#endif

            SyntaxTree tree = CSharpSyntaxTree.ParseText(fileContent);

            // GetRootAsync exists and is cancellable – small win
            SyntaxNode root = await tree.GetRootAsync(cancellationToken);

            // Process this file independently
            var localCollection = new CognitiveMetricsCollection();
            AnalyseClasses(configuration, root, localCollection, file);

            // Add all metrics from this file to the shared bag
            foreach (var metric in localCollection)
            {
                allMetrics.Add(metric);
            }
        });

        await Task.WhenAll(fileTasks);

        // Convert to the expected collection type
        var result = new CognitiveMetricsCollection();
        foreach (var metric in allMetrics)
        {
            result.Add(metric);
        }

        return result;
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
#if ROSLYN_4_0_OR_GREATER
        return classNode.Ancestors()
            .OfType<NamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .Concat(classNode.Ancestors()
                .OfType<FileScopedNamespaceDeclarationSyntax>()
                .Select(ns => ns.Name.ToString())
            )
            .Reverse();
#else
        return classNode.Ancestors()
            .OfType<NamespaceDeclarationSyntax>()
            .Select(n => n.Name.ToString())
            .Reverse();
#endif
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
}
