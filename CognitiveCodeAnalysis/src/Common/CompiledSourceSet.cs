/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;

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

    public static Task<CompiledSourceSet> BuildAsync(
        IReadOnlyList<string> files,
        CancellationToken cancellationToken = default
    ) => BuildAsync(files, progress: null, cancellationToken);

    public static async Task<CompiledSourceSet> BuildAsync(
        IReadOnlyList<string> files,
        IProgress<AnalysisProgress>? progress,
        CancellationToken cancellationToken = default
    ) {
        if (files.Count == 0)
        {
            return new CompiledSourceSet([], [], CreateCompilation([]));
        }

        int totalFiles = files.Count;
        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.CompilingSources,
            TotalFiles: totalFiles,
            ProcessedFiles: 0
        ));

        var readTasks = files.Select(async file =>
            (File: file, Content: await File.ReadAllTextAsync(file, cancellationToken)));
        (string File, string Content)[] sources = await Task.WhenAll(readTasks);

        var syntaxTrees = new SyntaxTree[sources.Length];
        int processedFiles = 0;

        Parallel.For(0, sources.Length, index =>
        {
            (string file, string content) = sources[index];
            syntaxTrees[index] = CSharpSyntaxTree.ParseText(content, path: file, cancellationToken: cancellationToken);

            int count = Interlocked.Increment(ref processedFiles);
            progress?.Report(new AnalysisProgress(
                AnalysisProgressPhase.CompilingSources,
                TotalFiles: totalFiles,
                ProcessedFiles: count
            ));
        });

        CSharpCompilation compilation = CreateCompilation(syntaxTrees);

        progress?.Report(new AnalysisProgress(
            AnalysisProgressPhase.CompilationCompleted,
            TotalFiles: totalFiles,
            ProcessedFiles: totalFiles
        ));

        return new CompiledSourceSet(files, syntaxTrees, compilation);
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
