/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace CognitiveCodeAnalysis.Tests.Reporting;

/// <summary>
/// Compiles small plugin assemblies during a test, so that the tests need no second project. This file is also linked
/// into the console app tests.
/// </summary>
internal static class PluginBuilder
{
    /// <summary>
    /// Compiles <paramref name="source"/> to <c>&lt;directory&gt;/&lt;name&gt;.dll</c>. The references are everything the test
    /// process runs with, which includes the framework and the tool's library.
    /// </summary>
    internal static string Compile(string directory, string name, string source, params string[] extraReferences)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + ".dll");
        string trusted = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        IEnumerable<MetadataReference> references = trusted
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(extraReferences)
            .Select(file => (MetadataReference)MetadataReference.CreateFromFile(file));

        CSharpCompilation compilation = CSharpCompilation.Create(
            name,
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var result = compilation.Emit(path);
        Assert.That(result.Success, Is.True, string.Join("\n", result.Diagnostics));
        return path;
    }

    /// <summary>A reporter for the analyse command that lists every method and the settings it was given.</summary>
    internal const string CognitiveReporter = """
        using System.Linq;
        using CognitiveCodeAnalysis.Reporting.Spi;

        namespace Acme;

        public class ListingReporter : ICustomCognitiveReporter
        {
            public string Render(CognitiveReportInput input) =>
                $"threshold={input.Settings.ScoreThreshold};baseline={input.HasBaseline}\n"
                + string.Join("\n", input.Methods.Select(m =>
                    $"{m.FilePath}|{m.ClassName}.{m.MethodName}|{m.Line}|{m.TotalScore}|ifs={m.Ifs.Count}/{m.Ifs.Score}|delta={m.Deltas?.TotalScore}"));
        }
        """;

    /// <summary>A reporter for the churn command that lists every class.</summary>
    internal const string ChurnReporter = """
        using System.Linq;
        using CognitiveCodeAnalysis.Reporting.Spi;

        namespace Acme;

        public class ClassListing : ICustomChurnReporter
        {
            public string Render(ChurnReportInput input) =>
                $"since={input.Since:yyyy-MM-dd};coverage={input.CoverageProvided}\n"
                + string.Join("\n", input.Classes.Select(c => $"{c.ClassName}|{c.FilePath}|{c.TimesChanged}|{c.Churn}|{c.RiskLevel}"));
        }
        """;
}
