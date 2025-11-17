using System.Collections.ObjectModel;

using CognitiveCodeAnalysis.Configuration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CognitiveCodeAnalysis.CognitiveAnalysis
{
    public class CognitiveCodeAnalyser
    {
        public Collection<CognitiveMetrics> AnalyzeFiles(List<string> files, CognitiveConfiguration configuration)
        {
            var metricsCollection = new Collection<CognitiveMetrics>();

            foreach (string file in files)
            {
                string fileContent = File.ReadAllText(file);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(fileContent);
                var root = tree.GetRoot();

                // Find all classes
                var classNodes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
                foreach (var classNode in classNodes)
                {
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

                        int parameterCount = methodNode.ParameterList.Parameters.Count;

                        int linesOfCode = methodNode.GetLocation()
                                                    .GetLineSpan()
                                                    .EndLinePosition.Line
                                      - methodNode.GetLocation()
                                                  .GetLineSpan()
                                                  .StartLinePosition.Line + 1;

                        string signature = methodNode.Modifiers.ToString() + " " +
                                           methodNode.ReturnType.ToString() + " " +
                                           methodNode.Identifier.Text +
                                           methodNode.ParameterList.ToString();

                        int nestingLevels = CalculateNestingLevels(methodNode, configuration);

                        metricsCollection.Add(new CognitiveMetrics(
                            methodName: methodNode.Identifier.Text,
                            className: fullClassName,
                            filePath: file,
                            signature: signature,
                            methodLineNumber: methodNode.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                            ifCount: ifCount,
                            argumentCount: parameterCount,
                            linesOfCode: linesOfCode,
                            elseCount: elseCount,
                            tryCatchCount: tryCount,
                            returnCount: methodNode.DescendantNodes().OfType<ReturnStatementSyntax>().Count(),
                            nestingLevels: nestingLevels
                        ));
                    }
                }
            }

            return metricsCollection;
        }

        // Combine namespace, containing types, and class name
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
            var body = methodNode.Body;
            if (body == null)
            {
                // Expression-bodied methods have no body block
                return 0;
            }

            int maxDepth = 0;
            CalculateNestingDepth(body, 0, ref maxDepth, null, configuration);
            return maxDepth;
        }

        private static void CalculateNestingDepth(SyntaxNode node, int currentDepth, ref int maxDepth, SyntaxNode? parent, CognitiveConfiguration configuration)
        {
            // Skip BlockSyntax nodes - they don't increase nesting depth
            if (node is BlockSyntax)
            {
                // Process children but don't increment depth for the block itself
                foreach (var child in node.ChildNodes())
                {
                    CalculateNestingDepth(child, currentDepth, ref maxDepth, node, configuration);
                }
                return;
            }

            // Handle ElseClauseSyntax based on configuration
            if (node is ElseClauseSyntax elseClause)
            {
                int depthForElse = currentDepth;
                if (configuration.CountElseAsNesting)
                {
                    depthForElse++;
                    maxDepth = Math.Max(maxDepth, depthForElse);
                }

                // Process children - if this else contains an if (else-if), we need to track the parent if depth
                foreach (var child in elseClause.ChildNodes())
                {
                    if (child is IfStatementSyntax && !configuration.CountElseIfAsNesting)
                    {
                        // else-if: use the same depth as the parent if statement
                        // The parent if statement's depth is currentDepth (before the else clause)
                        CalculateNestingDepth(child, currentDepth, ref maxDepth, elseClause, configuration);
                    }
                    else
                    {
                        CalculateNestingDepth(child, depthForElse, ref maxDepth, elseClause, configuration);
                    }
                }
                return;
            }

            // Handle IfStatementSyntax
            if (node is IfStatementSyntax ifStatement)
            {
                // Check if this if statement is inside an else clause (else-if pattern)
                bool isElseIf = parent is ElseClauseSyntax;

                if (isElseIf && !configuration.CountElseIfAsNesting)
                {
                    // else-if chains are flat - use the same depth as the parent if statement
                    // currentDepth already has the correct depth (passed from the else clause handling)
                }
                else
                {
                    // Normal if statement - increment depth
                    currentDepth++;
                }

                maxDepth = Math.Max(maxDepth, currentDepth);

                // Process children
                foreach (var child in ifStatement.ChildNodes())
                {
                    CalculateNestingDepth(child, currentDepth, ref maxDepth, ifStatement, configuration);
                }
                return;
            }

            // Handle other nesting nodes
            if (IsNestingNode(node))
            {
                currentDepth++;
                maxDepth = Math.Max(maxDepth, currentDepth);
            }

            // Recursively process child nodes
            foreach (var child in node.ChildNodes())
            {
                CalculateNestingDepth(child, currentDepth, ref maxDepth, node, configuration);
            }
        }

        private static bool IsNestingNode(SyntaxNode node)
        {
            // Control flow structures that create nesting levels
            // Exclude ElseClauseSyntax and IfStatementSyntax as they're handled separately
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
}
