using System.Collections.ObjectModel;

using CognitiveCodeAnalysis.CognitiveAnalysis;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using Newtonsoft.Json.Linq;

using static System.Runtime.InteropServices.JavaScript.JSType;

namespace CognitiveCodeAnalysis
{
    public class FileFinder
    {
        public Collection<CognitiveMetrics> Find(string[] directories)
        {
            var files = new List<string>();
            var metricsCollection = new Collection<CognitiveMetrics>();

            foreach (string directory in directories)
            {
                if (!Directory.Exists(directory)) {
                    continue;
                }

                files.AddRange(Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories));
            }

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
                            nestingLevels: methodNode.DescendantNodes()
                                                        .OfType<SyntaxNode>()
                                                        .Select(n => n.Ancestors().Count())
                                                        .DefaultIfEmpty(0)
                                                        .Max()
                        ));
                    }
                }
            }

            return metricsCollection;
        }

        // Combine namespace, containing types, and class name
        private static string GetFullyQualifiedClassName(ClassDeclarationSyntax classNode)
        {
            IEnumerable<string> containingTypes = collectNestedClasses(classNode);
            IEnumerable<string> namespaceParts = collectNamespaceParts(classNode);

            return string.Join(".", namespaceParts
                .Concat(containingTypes)
                .Append(classNode.Identifier.Text)
                .Where(p => !string.IsNullOrEmpty(p))
            );
        }

        private static IEnumerable<string> collectNestedClasses(ClassDeclarationSyntax classNode)
        {
            return classNode.Ancestors()
                .OfType<ClassDeclarationSyntax>()
                .Reverse()
                .Select(c => c.Identifier.Text);
        }

        // Collect namespace parts (including file-scoped namespaces)
        private static IEnumerable<string> collectNamespaceParts(ClassDeclarationSyntax classNode)
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
    }
}
