/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Collections.Immutable;

using CognitiveCodeAnalysis.Common;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CognitiveCodeAnalysis.CouplingAnalysis;

public class ClassCouplingAnalyser
{
    public IReadOnlyList<ClassCouplingMetrics> Analyse(IReadOnlyList<string> files)
    {
        if (files.Count == 0)
        {
            return [];
        }

        var sourceFileSet = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
        var syntaxTrees = new List<SyntaxTree>(files.Count);

        foreach (string file in files)
        {
            string fileContent = File.ReadAllText(file);
            syntaxTrees.Add(CSharpSyntaxTree.ParseText(fileContent, path: file));
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "CouplingAnalysis",
            syntaxTrees: syntaxTrees,
            references: RoslynMetadataReferences.Get(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
        );

        var sourceTypes = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        var typeDeclarations = new List<(SyntaxNode TypeNode, SemanticModel Model, INamedTypeSymbol TypeSymbol)>();

        foreach (SyntaxTree tree in syntaxTrees)
        {
            SemanticModel? model = compilation.GetSemanticModel(tree);
            if (model == null)
            {
                continue;
            }

            SyntaxNode root = tree.GetRoot();
            foreach (SyntaxNode typeNode in GetTypeDeclarations(root))
            {
                if (model.GetDeclaredSymbol(typeNode) is not INamedTypeSymbol typeSymbol)
                {
                    continue;
                }

                if (!IsSourceDefinedInFiles(typeSymbol, sourceFileSet))
                {
                    continue;
                }

                string typeKey = GetTypeKey(typeSymbol);
                if (!sourceTypes.ContainsKey(typeKey))
                {
                    sourceTypes.Add(typeKey, typeSymbol);
                }
                typeDeclarations.Add((typeNode, model, typeSymbol));
            }
        }

        var outgoingByKey = sourceTypes.Keys.ToDictionary(key => key, _ => new HashSet<string>(), StringComparer.Ordinal);

        foreach ((SyntaxNode typeNode, SemanticModel model, INamedTypeSymbol typeSymbol) in typeDeclarations)
        {
            string typeKey = GetTypeKey(typeSymbol);
            CollectDependencies(typeNode, model, typeSymbol, sourceTypes, outgoingByKey[typeKey]);
        }

        var incomingCounts = sourceTypes.Keys.ToDictionary(key => key, _ => 0, StringComparer.Ordinal);

        foreach (KeyValuePair<string, HashSet<string>> entry in outgoingByKey)
        {
            HashSet<string> targets = entry.Value;
            foreach (string targetKey in targets)
            {
                if (incomingCounts.ContainsKey(targetKey))
                {
                    incomingCounts[targetKey]++;
                }
            }
        }

        var results = new List<ClassCouplingMetrics>(sourceTypes.Count);

        foreach (string typeKey in sourceTypes.Keys)
        {
            int outgoing = outgoingByKey[typeKey].Count;
            int incoming = incomingCounts[typeKey];
            double stability = incoming + outgoing > 0
                ? (double)incoming / (incoming + outgoing)
                : 0;

            results.Add(new ClassCouplingMetrics
            {
                ClassName = typeKey,
                IncomingCoupling = incoming,
                OutgoingCoupling = outgoing,
                Stability = stability,
            });
        }

        return results;
    }

    private static IEnumerable<SyntaxNode> GetTypeDeclarations(SyntaxNode root)
    {
        foreach (ClassDeclarationSyntax node in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            yield return node;
        }

#if ROSLYN_4_0_OR_GREATER
        foreach (RecordDeclarationSyntax node in root.DescendantNodes().OfType<RecordDeclarationSyntax>())
        {
            yield return node;
        }
#endif

        foreach (StructDeclarationSyntax node in root.DescendantNodes().OfType<StructDeclarationSyntax>())
        {
            yield return node;
        }

        foreach (InterfaceDeclarationSyntax node in root.DescendantNodes().OfType<InterfaceDeclarationSyntax>())
        {
            yield return node;
        }
    }

    private static bool IsSourceDefinedInFiles(INamedTypeSymbol symbol, HashSet<string> sourceFiles)
    {
        return symbol.Locations.Any(location =>
            location.IsInSource
            && location.SourceTree?.FilePath != null
            && sourceFiles.Contains(location.SourceTree.FilePath)
        );
    }

    private static string GetTypeKey(INamedTypeSymbol symbol)
    {
        var typeParts = new Stack<string>();
        INamedTypeSymbol? current = symbol;
        while (current != null)
        {
            typeParts.Push(current.Name);
            current = current.ContainingType;
        }

        string typePath = string.Join(".", typeParts);
        string ns = symbol.ContainingNamespace?.ToDisplayString() ?? string.Empty;

        return string.IsNullOrEmpty(ns) ? typePath : $"{ns}.{typePath}";
    }

    private static void CollectDependencies(
        SyntaxNode typeRoot,
        SemanticModel model,
        INamedTypeSymbol declaringType,
        Dictionary<string, INamedTypeSymbol> sourceTypes,
        HashSet<string> dependencies
    ) {
        string declaringKey = GetTypeKey(declaringType);

        if (declaringType.BaseType is { SpecialType: not SpecialType.System_Object } baseType)
        {
            TryAddDependency(baseType, declaringKey, sourceTypes, dependencies);
        }

        foreach (INamedTypeSymbol iface in declaringType.Interfaces)
        {
            TryAddDependency(iface, declaringKey, sourceTypes, dependencies);
        }

        foreach (SyntaxNode node in typeRoot.DescendantNodesAndSelf())
        {
            if (node is not TypeSyntax typeSyntax)
            {
                continue;
            }

            SymbolInfo symbolInfo = model.GetSymbolInfo(typeSyntax);
            if (symbolInfo.Symbol is INamedTypeSymbol namedType)
            {
                TryAddDependency(namedType, declaringKey, sourceTypes, dependencies);
                continue;
            }

            foreach (ISymbol candidate in symbolInfo.CandidateSymbols)
            {
                if (candidate is INamedTypeSymbol candidateType)
                {
                    TryAddDependency(candidateType, declaringKey, sourceTypes, dependencies);
                }
            }
        }
    }

    private static void TryAddDependency(
        INamedTypeSymbol referencedType,
        string declaringKey,
        Dictionary<string, INamedTypeSymbol> sourceTypes,
        HashSet<string> dependencies
    ) {
        INamedTypeSymbol definition = referencedType;
        if (referencedType is { TypeKind: TypeKind.Error })
        {
            return;
        }

        if (referencedType.OriginalDefinition != null)
        {
            definition = referencedType.OriginalDefinition;
        }

        string targetKey = GetTypeKey(definition);
        if (targetKey == declaringKey)
        {
            return;
        }

        if (sourceTypes.ContainsKey(targetKey))
        {
            dependencies.Add(targetKey);
        }
    }

}
