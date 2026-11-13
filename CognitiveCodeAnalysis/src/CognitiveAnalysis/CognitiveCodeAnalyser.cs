/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Collections.Concurrent;
using System.Text.RegularExpressions;

using CognitiveCodeAnalysis.Common;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.HalsteadAnalysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

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
    ) => AnalyseFiles(files, configuration, progress: null);

    public CognitiveMetricsCollection AnalyseFiles(
        List<string> files,
        CognitiveConfiguration configuration,
        IProgress<AnalysisProgress>? progress
    ) => AnalyseFilesAsync(files, configuration, progress).GetAwaiter().GetResult();

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
    ) => await AnalyseFilesAsync(files, configuration, progress: null, cancellationToken);

    public async Task<CognitiveMetricsCollection> AnalyseFilesAsync(
        List<string> files,
        CognitiveConfiguration configuration,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken = default
    ) {
        CompiledSourceSet sources = await CompiledSourceSet.BuildAsync(files, cancellationToken);
        return AnalyseCompiled(sources, configuration, progress, cancellationToken);
    }

    /// <summary>
    /// Analyses an already parsed and compiled set of sources. The shared
    /// <see cref="CompiledSourceSet"/> lets the semantic model reuse a single
    /// compilation instead of building one per file.
    /// </summary>
    public CognitiveMetricsCollection AnalyseCompiled(
        CompiledSourceSet sources,
        CognitiveConfiguration configuration,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken = default
    ) {
        int totalFiles = sources.SyntaxTrees.Count;
        int processedFiles = 0;

        progress?.Report(new AnalysisProgress(AnalysisProgressPhase.AnalysingFiles, TotalFiles: totalFiles));

        var allMetrics = new ConcurrentBag<CognitiveMetrics>();
        Regex[] excludePatterns = RegexPatterns.Compile(configuration.ExcludePatterns);

        Parallel.ForEach(
            sources.SyntaxTrees,
            new ParallelOptions { CancellationToken = cancellationToken },
            tree =>
            {
                try
                {
                    SyntaxNode root = tree.GetRoot(cancellationToken);
                    SemanticModel semanticModel = sources.Compilation.GetSemanticModel(tree);

                    var localCollection = new CognitiveMetricsCollection();
                    AnalyseClasses(configuration, root, localCollection, tree.FilePath, semanticModel, excludePatterns);

                    foreach (CognitiveMetrics metric in localCollection)
                    {
                        allMetrics.Add(metric);
                    }
                }
                finally
                {
                    int count = Interlocked.Increment(ref processedFiles);
                    progress?.Report(new AnalysisProgress(
                        AnalysisProgressPhase.AnalysingFiles,
                        TotalFiles: totalFiles,
                        ProcessedFiles: count
                    ));
                }
            });

        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.AnalysisCompleted,
            TotalFiles: totalFiles,
            ProcessedFiles: totalFiles
        ));

        // Files are analysed in parallel; sort so every run and every report sees the same order.
        var result = new CognitiveMetricsCollection();
        foreach (CognitiveMetrics metric in allMetrics
            .OrderBy(m => m.FilePath, StringComparer.Ordinal)
            .ThenBy(m => m.methodLineNumber)
            .ThenBy(m => m.ClassName, StringComparer.Ordinal))
        {
            result.Add(metric);
        }

        return result;
    }

    private const string IgnoreTag = "@cca-ignore";

    private static CognitiveMetricsCollection AnalyseClasses(
        CognitiveConfiguration configuration,
        SyntaxNode root,
        CognitiveMetricsCollection metricsCollection,
        string file,
        SemanticModel? semanticModel,
        Regex[] excludePatterns
    ) {
        // Classes, structs, records and interfaces; an ignored type also hides every type nested in it.
        IEnumerable<TypeDeclarationSyntax> typeNodes = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .Where(type => !type.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Any(t => IsIgnored(t, t.Identifier)));

        foreach (TypeDeclarationSyntax typeNode in typeNodes)
        {
            ExtractMetricsFromType(configuration, typeNode, metricsCollection, file, semanticModel, excludePatterns);
        }

        return metricsCollection;
    }

    /// <summary>
    /// <![CDATA[
    /// A declaration is ignored when the tag is in a comment between the previous declaration and its name
    /// (XML doc comments, comments after an opening brace, above or between attributes and modifiers),
    /// or in a comment at the end of its last line. Comments inside the body do not count.
    /// A comment after the end of a declaration belongs to the declaration that starts after it on the same line, if any.
    /// ]]>
    /// </summary>
    private static bool IsIgnored(MemberDeclarationSyntax declaration, SyntaxToken identifier)
    {
        SyntaxToken first = declaration.GetFirstToken();
        SyntaxToken previous = first.GetPreviousToken();
        SyntaxToken last = declaration.GetLastToken();

        IEnumerable<SyntaxTrivia> trivia = declaration.DescendantTrivia(TextSpan.FromBounds(declaration.FullSpan.Start, identifier.SpanStart));
        if (!previous.IsKind(SyntaxKind.None) && (!EndsANode(previous) || OnSameLine(previous, first)))
        {
            trivia = trivia.Concat(previous.TrailingTrivia);
        }

        if (!StartsADeclarationOnTheSameLine(last.GetNextToken(), last))
        {
            trivia = trivia.Concat(last.TrailingTrivia);
        }

        return trivia.Any(t => IsComment(t) && t.ToFullString().Contains(IgnoreTag, StringComparison.Ordinal));
    }

    private static bool EndsANode(SyntaxToken token) =>
        token.Parent!.AncestorsAndSelf().Any(node => node.GetLastToken() == token);

    private static bool StartsADeclarationOnTheSameLine(SyntaxToken next, SyntaxToken last) =>
        !next.IsKind(SyntaxKind.None)
        && OnSameLine(last, next)
        && next.Parent!.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().Any(member => member.GetFirstToken() == next);

    private static bool OnSameLine(SyntaxToken earlier, SyntaxToken later) =>
        earlier.GetLocation().GetLineSpan().EndLinePosition.Line == later.GetLocation().GetLineSpan().StartLinePosition.Line;

    private static bool IsComment(SyntaxTrivia trivia) =>
        trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineCommentTrivia)
        || trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
        || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia);

    /// <summary>
    /// <![CDATA[
    /// Methods and constructors are the analysed units. Interface members only count when they have a body.
    /// ]]>
    /// </summary>
    private static IEnumerable<(BaseMethodDeclarationSyntax Node, SyntaxToken Identifier)> AnalysedUnits(TypeDeclarationSyntax typeNode)
    {
        bool isInterface = typeNode is InterfaceDeclarationSyntax;

        foreach (MemberDeclarationSyntax member in typeNode.Members)
        {
            (BaseMethodDeclarationSyntax Node, SyntaxToken Identifier)? unit = member switch
            {
                MethodDeclarationSyntax method => (method, method.Identifier),
                ConstructorDeclarationSyntax constructor => (constructor, constructor.Identifier),
                _ => null,
            };

            if (unit is not { } found
                || (isInterface && found.Node.Body is null && found.Node.ExpressionBody is null)
                || IsIgnored(found.Node, found.Identifier))
            {
                continue;
            }

            yield return found;
        }
    }

    private readonly record struct MethodNodeCounts(
        int IfCount,
        int ElseCount,
        int TryCount,
        int LoopCount,
        int SwitchCount,
        int SwitchSectionCount,
        int ReturnCount,
        int LocalVariableCount,
        int CatchCount,
        int ConditionalExpressionCount,
        int LogicalAndOrCount
    )
    {
        public int CyclomaticComplexity =>
            IfCount + LoopCount + SwitchSectionCount + CatchCount +
            ConditionalExpressionCount + LogicalAndOrCount + 1;
    }

    private static MethodNodeCounts CountMethodNodes(BaseMethodDeclarationSyntax methodNode)
    {
        int ifCount = 0, elseCount = 0, tryCount = 0, loopCount = 0,
            switchCount = 0, switchSectionCount = 0, returnCount = 0,
            localVarCount = 0, catchCount = 0, conditionalCount = 0, logicalAndOrCount = 0;

        foreach (SyntaxNode node in methodNode.DescendantNodes())
        {
            switch (node)
            {
                case IfStatementSyntax:
                    ifCount++;
                    break;
                case ElseClauseSyntax:
                    elseCount++;
                    break;
                case TryStatementSyntax:
                    tryCount++;
                    break;
                case ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax:
                    loopCount++;
                    break;
                case CommonForEachStatementSyntax forEach:
                    // The iteration variables (one, or several when deconstructing) have no VariableDeclarator node.
                    loopCount++;
                    localVarCount += CountForEachVariables(forEach);
                    break;
                case SwitchStatementSyntax sw:
                    switchCount++;
                    switchSectionCount += sw.Sections.Count;
                    break;
                case ReturnStatementSyntax:
                    returnCount++;
                    break;
                case VariableDeclaratorSyntax declarator when !IsMethodParameter(declarator, methodNode):
                    localVarCount++;
                    break;
                case CatchClauseSyntax catchClause:
                    catchCount++;
                    if (!string.IsNullOrEmpty(catchClause.Declaration?.Identifier.ValueText))
                    {
                        localVarCount++;
                    }

                    break;
                case ConditionalExpressionSyntax:
                    conditionalCount++;
                    break;
                case BinaryExpressionSyntax binary
                    when binary.OperatorToken.IsKind(SyntaxKind.AmpersandAmpersandToken)
                      || binary.OperatorToken.IsKind(SyntaxKind.BarBarToken):
                    logicalAndOrCount++;
                    break;
            }
        }

        return new MethodNodeCounts(
            ifCount, elseCount, tryCount, loopCount, switchCount, switchSectionCount,
            returnCount, localVarCount, catchCount, conditionalCount, logicalAndOrCount
        );
    }

    private static int CountForEachVariables(CommonForEachStatementSyntax forEach) =>
        forEach is ForEachVariableStatementSyntax deconstructing
            ? deconstructing.Variable.DescendantNodesAndSelf().OfType<SingleVariableDesignationSyntax>().Count()
            : 1;

    private static void ExtractMetricsFromType(
        CognitiveConfiguration configuration,
        TypeDeclarationSyntax typeNode,
        CognitiveMetricsCollection metricsCollection,
        string file,
        SemanticModel? semanticModel,
        Regex[] excludePatterns
    ) {
        string fullClassName = GetFullyQualifiedClassName(typeNode);

        foreach ((BaseMethodDeclarationSyntax methodNode, SyntaxToken identifier) in AnalysedUnits(typeNode))
        {
            string methodName = identifier.Text;
            string unitName = fullClassName + "::" + methodName;
            if (excludePatterns.Any(pattern => pattern.IsMatch(unitName)))
            {
                continue;
            }

            MethodNodeCounts counts = CountMethodNodes(methodNode);
            (int fieldAccessCount, int propertyAccessCount) = CountFieldAndPropertyAccesses(methodNode, semanticModel);

            HalsteadMetrics halstead = HalsteadSyntaxCollector.CollectForMethod(methodNode, unitName);

            metricsCollection.Add(new CognitiveMetrics(
                methodName: methodName,
                className: fullClassName,
                filePath: file,
                methodSignature: GetFullSignature(methodNode, methodName),
                methodLineNumber: methodNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                ifCount: counts.IfCount,
                argumentCount: methodNode.ParameterList.Parameters.Count,
                linesOfCode: GetLinesOfCode(methodNode),
                elseCount: counts.ElseCount,
                loopCount: counts.LoopCount,
                switchCount: counts.SwitchCount,
                tryCatchCount: counts.TryCount,
                returnCount: counts.ReturnCount,
                nestingLevels: CalculateNestingLevels(methodNode, configuration),
                cyclomaticComplexity: counts.CyclomaticComplexity,
                localVariableCount: counts.LocalVariableCount,
                fieldAccessCount: fieldAccessCount,
                propertyAccessCount: propertyAccessCount,
                halstead: halstead
            ));
        }
    }

    private static bool IsMethodParameter(VariableDeclaratorSyntax declarator, BaseMethodDeclarationSyntax methodNode)
    {
        return declarator.Ancestors()
            .OfType<ParameterSyntax>()
            .Any(parameter => parameter.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault() == methodNode);
    }

    private static (int FieldAccessCount, int PropertyAccessCount) CountFieldAndPropertyAccesses(
        BaseMethodDeclarationSyntax methodNode,
        SemanticModel? semanticModel
    ) {
        if (semanticModel == null)
        {
            return (0, 0);
        }

        int fieldAccessCount = 0;
        int propertyAccessCount = 0;

        foreach (MemberAccessExpressionSyntax memberAccess in methodNode.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            ClassifyMemberSymbol(semanticModel.GetSymbolInfo(memberAccess).Symbol, ref fieldAccessCount, ref propertyAccessCount);
        }

        foreach (IdentifierNameSyntax identifier in methodNode.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            if (identifier.Parent is MemberAccessExpressionSyntax memberAccess && memberAccess.Name == identifier)
            {
                continue;
            }

            if (identifier.Parent is MemberBindingExpressionSyntax)
            {
                continue;
            }

            ClassifyMemberSymbol(semanticModel.GetSymbolInfo(identifier).Symbol, ref fieldAccessCount, ref propertyAccessCount);
        }

        return (fieldAccessCount, propertyAccessCount);
    }

    private static void ClassifyMemberSymbol(ISymbol? symbol, ref int fieldAccessCount, ref int propertyAccessCount)
    {
        switch (symbol)
        {
            case IFieldSymbol:
                fieldAccessCount++;
                break;
            case IPropertySymbol:
                propertyAccessCount++;
                break;
        }
    }

    private static string GetFullSignature(BaseMethodDeclarationSyntax node, string name)
    {
        if (node is not MethodDeclarationSyntax methodNode)
        {
            return (node.Modifiers.ToString() + " " + name + node.ParameterList).TrimStart();
        }

        string typeParameters = methodNode.TypeParameterList?.ToString() ?? string.Empty;
        string constraints = methodNode.ConstraintClauses.Count > 0
            ? " " + string.Join(" ", methodNode.ConstraintClauses.Select(c => c.ToString()))
            : string.Empty;

        return string.Concat(
            methodNode.Modifiers.ToString(),
            " ",
            methodNode.ReturnType.ToString(),
            " ",
            methodNode.Identifier.Text,
            typeParameters,
            methodNode.ParameterList.ToString(),
            constraints);
    }

    private static int GetLinesOfCode(BaseMethodDeclarationSyntax methodNode)
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
    private static string GetFullyQualifiedClassName(TypeDeclarationSyntax classNode)
    {
        IEnumerable<string> containingTypes = CollectNestedClasses(classNode);
        IEnumerable<string> namespaceParts = CollectNamespaceParts(classNode);

        return string.Join(".", namespaceParts
            .Concat(containingTypes)
            .Append(classNode.Identifier.Text)
            .Where(p => !string.IsNullOrEmpty(p))
        );
    }

    private static IEnumerable<string> CollectNestedClasses(TypeDeclarationSyntax classNode)
    {
        return classNode.Ancestors()
            .OfType<TypeDeclarationSyntax>()
            .Reverse()
            .Select(c => c.Identifier.Text);
    }

    // Collect namespace parts (including file-scoped namespaces)
    private static IEnumerable<string> CollectNamespaceParts(TypeDeclarationSyntax classNode)
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

    private static int CalculateNestingLevels(BaseMethodDeclarationSyntax methodNode, CognitiveConfiguration configuration)
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

        int childDepth = currentDepth;
        if (IsNestingNode(node))
        {
            childDepth = currentDepth + 1;
            maxDepth = Math.Max(maxDepth, childDepth);
        }

        ProcessChildNodes(node, childDepth, ref maxDepth, configuration);
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
    /// Catch and finally clauses belong to their try statement and share its level.
    /// ]]>
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    private static bool IsNestingNode(SyntaxNode node)
    {
        return node is ForStatementSyntax
            || node is CommonForEachStatementSyntax
            || node is WhileStatementSyntax
            || node is DoStatementSyntax
            || node is SwitchStatementSyntax
            || node is TryStatementSyntax
            || node is LockStatementSyntax
            || node is UsingStatementSyntax
            || node is FixedStatementSyntax
            || node is CheckedStatementSyntax
            || node is UnsafeStatementSyntax;
    }
}
