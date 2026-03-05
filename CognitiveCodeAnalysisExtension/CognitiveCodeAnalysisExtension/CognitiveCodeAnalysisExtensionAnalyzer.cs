using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace CognitiveCodeAnalysisExtension
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class CognitiveCodeAnalysisExtensionAnalyzer : DiagnosticAnalyzer
    {
        public const string MethodDiagnosticId = "CognitiveComplexityMethod";
        public const string ClassDiagnosticId = "CognitiveComplexityClass";

        // Method diagnostic
        private static readonly LocalizableString MethodTitle = new LocalizableResourceString(nameof(Resources.MethodAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MethodMessageFormat = new LocalizableResourceString(nameof(Resources.MethodAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MethodDescription = new LocalizableResourceString(nameof(Resources.MethodAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

        // Class diagnostic
        private static readonly LocalizableString ClassTitle = new LocalizableResourceString(nameof(Resources.ClassAnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString ClassMessageFormat = new LocalizableResourceString(nameof(Resources.ClassAnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString ClassDescription = new LocalizableResourceString(nameof(Resources.ClassAnalyzerDescription), Resources.ResourceManager, typeof(Resources));

        private const string Category = "Maintainability";

        private static readonly DiagnosticDescriptor MethodRule = new DiagnosticDescriptor(MethodDiagnosticId, MethodTitle, MethodMessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: MethodDescription);
        private static readonly DiagnosticDescriptor ClassRule = new DiagnosticDescriptor(ClassDiagnosticId, ClassTitle, ClassMessageFormat, Category, DiagnosticSeverity.Info, isEnabledByDefault: true, description: ClassDescription);

        private static readonly CognitiveConfiguration _configuration = CreateDefaultConfiguration();

        private static CognitiveConfiguration CreateDefaultConfiguration()
        {
            return new CognitiveConfiguration
            {
                ScoreThreshold = 0.5,
                ShowOnlyMethodsExceedingThreshold = true,
                GroupByClass = true,
                CountElseAsNesting = false,
                CountElseIfAsNesting = false,
                Metrics = new Dictionary<string, MetricConfiguration>
                {
                    ["linesOfCode"] = new MetricConfiguration { Threshold = 60, Scale = 25.0, Enabled = true },
                    ["argumentCount"] = new MetricConfiguration { Threshold = 4, Scale = 1.0, Enabled = true },
                    ["returnCount"] = new MetricConfiguration { Threshold = 2, Scale = 5.0, Enabled = true },
                    ["variableCount"] = new MetricConfiguration { Threshold = 4, Scale = 5.0, Enabled = true },
                    ["propertyCallCount"] = new MetricConfiguration { Threshold = 4, Scale = 15.0, Enabled = true },
                    ["ifCount"] = new MetricConfiguration { Threshold = 3, Scale = 1.0, Enabled = true },
                    ["nestingLevels"] = new MetricConfiguration { Threshold = 1, Scale = 1.0, Enabled = true },
                    ["elseCount"] = new MetricConfiguration { Threshold = 1, Scale = 1.0, Enabled = true }
                }
            };
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MethodRule, ClassRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
            context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Find the containing class
            var classDeclaration = methodDeclaration.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDeclaration == null)
                return;

            try
            {
                // Extract metrics for this single method
                var metrics = ExtractMethodMetrics(methodDeclaration, classDeclaration, context.SemanticModel);

                // Calculate the score
                var calculator = new ScoreCalculator(_configuration);
                calculator.CalculateScores(metrics);

                // Report the diagnostic
                var diagnostic = Diagnostic.Create(MethodRule, methodDeclaration.Identifier.GetLocation(), metrics.totalScore.ToString("F1"));
                context.ReportDiagnostic(diagnostic);
            }
            catch (Exception)
            {
                // Silently ignore analysis errors to avoid breaking the IDE
            }
        }

        private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;

            try
            {
                // Get all methods in this class
                var methodDeclarations = classDeclaration.Members.OfType<MethodDeclarationSyntax>();

                double totalClassScore = 0;
                var calculator = new ScoreCalculator(_configuration);

                foreach (var methodDeclaration in methodDeclarations)
                {
                    var metrics = ExtractMethodMetrics(methodDeclaration, classDeclaration, context.SemanticModel);
                    calculator.CalculateScores(metrics);
                    totalClassScore += metrics.totalScore;
                }

                if (methodDeclarations.Any())
                {
                    // Report the diagnostic
                    var diagnostic = Diagnostic.Create(ClassRule, classDeclaration.Identifier.GetLocation(), totalClassScore.ToString("F1"));
                    context.ReportDiagnostic(diagnostic);
                }
            }
            catch (Exception)
            {
                // Silently ignore analysis errors to avoid breaking the IDE
            }
        }

        private static CognitiveMetrics ExtractMethodMetrics(MethodDeclarationSyntax methodDeclaration, ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel)
        {
            var className = GetFullyQualifiedClassName(classDeclaration);
            var methodName = methodDeclaration.Identifier.Text;
            var filePath = methodDeclaration.SyntaxTree.FilePath;
            var methodSignature = GetMethodSignature(methodDeclaration);
            var lineNumber = methodDeclaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

            // Extract basic metrics
            var ifCount = methodDeclaration.DescendantNodes().OfType<IfStatementSyntax>().Count();
            var elseCount = methodDeclaration.DescendantNodes().OfType<ElseClauseSyntax>().Count();
            var tryCatchCount = methodDeclaration.DescendantNodes().OfType<TryStatementSyntax>().Count();
            var returnCount = methodDeclaration.DescendantNodes().OfType<ReturnStatementSyntax>().Count();
            var argumentCount = methodDeclaration.ParameterList.Parameters.Count;
            var linesOfCode = GetLinesOfCode(methodDeclaration);
            var nestingLevels = CalculateNestingLevels(methodDeclaration, _configuration);

            return new CognitiveMetrics(
                methodName: methodName,
                className: className,
                filePath: filePath,
                methodSignature: methodSignature,
                methodLineNumber: lineNumber,
                ifCount: ifCount,
                elseCount: elseCount,
                tryCatchCount: tryCatchCount,
                returnCount: returnCount,
                argumentCount: argumentCount,
                linesOfCode: linesOfCode,
                nestingLevels: nestingLevels
            );
        }

        private static string GetFullyQualifiedClassName(ClassDeclarationSyntax classDeclaration)
        {
            var namespaceDeclaration = classDeclaration.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
            var namespaceName = namespaceDeclaration?.Name.ToString() ?? "";
            return string.IsNullOrEmpty(namespaceName) ? classDeclaration.Identifier.Text : $"{namespaceName}.{classDeclaration.Identifier.Text}";
        }

        private static string GetMethodSignature(MethodDeclarationSyntax methodDeclaration)
        {
            return $"{methodDeclaration.Modifiers} {methodDeclaration.ReturnType} {methodDeclaration.Identifier.Text}{methodDeclaration.ParameterList}";
        }

        private static int GetLinesOfCode(MethodDeclarationSyntax methodDeclaration)
        {
            var span = methodDeclaration.GetLocation().GetLineSpan();
            return span.EndLinePosition.Line - span.StartLinePosition.Line + 1;
        }

        private static int CalculateNestingLevels(MethodDeclarationSyntax methodDeclaration, CognitiveConfiguration configuration)
        {
            var body = methodDeclaration.Body;
            if (body == null) return 0;

            return CalculateNestingDepth(body, 0, configuration);
        }

        private static int CalculateNestingDepth(SyntaxNode node, int currentDepth, CognitiveConfiguration configuration)
        {
            var maxDepth = currentDepth;

            if (IsNestingNode(node))
            {
                maxDepth = Math.Max(maxDepth, currentDepth + 1);
                currentDepth++;
            }

            foreach (var child in node.ChildNodes())
            {
                maxDepth = Math.Max(maxDepth, CalculateNestingDepth(child, currentDepth, configuration));
            }

            return maxDepth;
        }

        private static bool IsNestingNode(SyntaxNode node)
        {
            return node is ForStatementSyntax ||
                   node is ForEachStatementSyntax ||
                   node is WhileStatementSyntax ||
                   node is DoStatementSyntax ||
                   node is IfStatementSyntax ||
                   node is SwitchStatementSyntax ||
                   node is TryStatementSyntax;
        }
    }
}
