using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CognitiveCodeAnalysisExtension
{
    [ExportCodeFixProvider(LanguageNames.CSharp , Name = nameof(CognitiveCodeAnalysisExtensionCodeFixProvider)), Shared]
    public class CognitiveCodeAnalysisExtensionCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            // Cognitive complexity diagnostics are informational only - no fixes available
            get { return ImmutableArray<string>.Empty; }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            // No code fixes are provided for cognitive complexity diagnostics
            // as they are informational only
            return Task.CompletedTask;
        }
    }
}
