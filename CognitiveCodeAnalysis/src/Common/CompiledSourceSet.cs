/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CognitiveCodeAnalysis.Common;

/// <summary>
/// Reads and parses a set of C# source files once and exposes a single shared
/// <see cref="CSharpCompilation"/>. Sharing one compilation avoids re-reading,
/// re-parsing and re-loading metadata references for every analyser.
/// </summary>
public sealed class CompiledSourceSet
{
    private CompiledSourceSet(
        IReadOnlyList<string> files,
        IReadOnlyList<SyntaxTree> syntaxTrees,
        CSharpCompilation compilation
    ) {
        Files = files;
        SyntaxTrees = syntaxTrees;
        Compilation = compilation;
    }

    public IReadOnlyList<string> Files { get; }

    public IReadOnlyList<SyntaxTree> SyntaxTrees { get; }

    public CSharpCompilation Compilation { get; }

    public static async Task<CompiledSourceSet> BuildAsync(
        IReadOnlyList<string> files,
        CancellationToken cancellationToken = default
    ) {
        if (files.Count == 0)
        {
            return new CompiledSourceSet([], [], CreateCompilation([]));
        }

        var readTasks = files.Select(async file =>
            (File: file, Content: await File.ReadAllTextAsync(file, cancellationToken)));
        (string File, string Content)[] sources = await Task.WhenAll(readTasks);

        var syntaxTrees = new SyntaxTree[sources.Length];
        Parallel.For(0, sources.Length, index =>
        {
            (string file, string content) = sources[index];
            syntaxTrees[index] = CSharpSyntaxTree.ParseText(content, path: file, cancellationToken: cancellationToken);
        });

        return new CompiledSourceSet(files, syntaxTrees, CreateCompilation(syntaxTrees));
    }

    private static CSharpCompilation CreateCompilation(IReadOnlyList<SyntaxTree> syntaxTrees)
    {
        return CSharpCompilation.Create(
            assemblyName: "CognitiveAnalysis",
            syntaxTrees: syntaxTrees,
            references: RoslynMetadataReferences.Get(),
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                concurrentBuild: true
            )
        );
    }
}
