using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
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

        private static readonly DiagnosticDescriptor MethodRule = new DiagnosticDescriptor(MethodDiagnosticId, MethodTitle, MethodMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: MethodDescription);
        private static readonly DiagnosticDescriptor ClassRule = new DiagnosticDescriptor(ClassDiagnosticId, ClassTitle, ClassMessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: ClassDescription);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(MethodRule, ClassRule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(startContext =>
            {
                CognitiveConfiguration configuration =
                    ConfigurationLoader.LoadCognitiveConfigurationForAnalyzer(startContext.Options.AdditionalFiles , startContext.CancellationToken);

                startContext.RegisterSyntaxNodeAction(ctx => AnalyzeMethod(ctx , configuration) , SyntaxKind.MethodDeclaration);
                startContext.RegisterSyntaxNodeAction(ctx => AnalyzeClass(ctx , configuration) , SyntaxKind.ClassDeclaration);
            });
        }

        /// <summary>Same score-threshold rule as <see cref="ReportMetricsFilter"/> for method rows.</summary>
        private static bool ShouldReportMethodDiagnostic(CognitiveConfiguration configuration , double totalScore)
        {
            if (!configuration.ShowOnlyMethodsExceedingThreshold)
            {
                return true;
            }

            return totalScore > configuration.ScoreThreshold;
        }

        /// <summary>
        /// When threshold filtering is on, aligns with CLI-style class stats: emit only if some method exceeds the score threshold.
        /// </summary>
        private static bool ShouldReportClassDiagnostic(CognitiveConfiguration configuration , IReadOnlyList<double> methodScores)
        {
            if (methodScores.Count == 0)
            {
                return false;
            }

            if (!configuration.ShowOnlyMethodsExceedingThreshold)
            {
                return true;
            }

            return methodScores.Any(s => s > configuration.ScoreThreshold);
        }

        private static void AnalyzeMethod(SyntaxNodeAnalysisContext context , CognitiveConfiguration configuration)
        {
            var methodDeclaration = (MethodDeclarationSyntax)context.Node;

            // Find the containing class
            var classDeclaration = methodDeclaration.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();
            if (classDeclaration == null)
                return;

            try
            {
                var metrics = ExtractMethodMetrics(methodDeclaration, classDeclaration, context.SemanticModel , configuration);

                var calculator = new ScoreCalculator(configuration);
                calculator.CalculateScores(metrics);

                if (!ShouldReportMethodDiagnostic(configuration , metrics.totalScore))
                {
                    return;
                }

                var diagnostic = Diagnostic.Create(MethodRule, methodDeclaration.Identifier.GetLocation(), metrics.totalScore.ToString("F1"));
                context.ReportDiagnostic(diagnostic);
            }
            catch (Exception)
            {
                // Silently ignore analysis errors to avoid breaking the IDE
            }
        }

        private static void AnalyzeClass(SyntaxNodeAnalysisContext context , CognitiveConfiguration configuration)
        {
            var classDeclaration = (ClassDeclarationSyntax)context.Node;

            try
            {
                IEnumerable<MethodDeclarationSyntax> methodDeclarations = classDeclaration.Members.OfType<MethodDeclarationSyntax>();

                double totalClassScore = 0;
                var calculator = new ScoreCalculator(configuration);

                var perMethodTotals = new List<double>();

                foreach (var methodDeclaration in methodDeclarations)
                {
                    CognitiveMetrics metrics = ExtractMethodMetrics(methodDeclaration, classDeclaration, context.SemanticModel , configuration);
                    calculator.CalculateScores(metrics);
                    totalClassScore += metrics.totalScore;
                    perMethodTotals.Add(metrics.totalScore);
                }

                if (ShouldReportClassDiagnostic(configuration , perMethodTotals))
                {
                    var diagnostic = Diagnostic.Create(ClassRule, classDeclaration.Identifier.GetLocation(), totalClassScore.ToString("F1"));
                    context.ReportDiagnostic(diagnostic);
                }
            }
            catch (Exception)
            {
                // Silently ignore analysis errors to avoid breaking the IDE
            }
        }

        private static CognitiveMetrics ExtractMethodMetrics(MethodDeclarationSyntax methodDeclaration, ClassDeclarationSyntax classDeclaration, SemanticModel semanticModel, CognitiveConfiguration configuration)
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
            var nestingLevels = CalculateNestingLevels(methodDeclaration, configuration);

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
