/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.RegularExpressions;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

/// <summary>
/// Compares rendered report output against committed golden fixture files.
/// Regenerate fixtures: <c>dotnet test --filter "FullyQualifiedName~RegenerateGoldenFixtures"</c>
/// </summary>
internal static class GoldenReportAssert
{
    private static readonly string GoldenRoot = Path.Combine(
        AppContext.BaseDirectory,
        "fixtures",
        "reports",
        "golden");

    internal static void AssertMatchesGolden(
        IReport report,
        CognitiveMetricsCollection collection,
        CognitiveConfiguration configuration,
        string goldenFileName,
        GoldenNormalization normalization = GoldenNormalization.None
    )
    {
        string actual = RenderReport(report, collection, configuration, normalization);
        string expectedPath = Path.Combine(GoldenRoot, goldenFileName);
        Assert.That(File.Exists(expectedPath), Is.True, $"Golden file not found: {expectedPath}");

        string expected = NormalizeLineEndings(File.ReadAllText(expectedPath));
        if (normalization == GoldenNormalization.SarifAssemblyVersion)
        {
            expected = NormalizeSarifVersion(expected);
        }

        Assert.That(actual, Is.EqualTo(expected));
    }

    internal static string RenderReport(
        IReport report,
        CognitiveMetricsCollection collection,
        CognitiveConfiguration configuration,
        GoldenNormalization normalization = GoldenNormalization.None
    )
    {
        string path = Path.Combine(Path.GetTempPath(), "golden-" + Guid.NewGuid() + ".out");
        try
        {
            report.RenderMetrics(path, collection, configuration);
            string content = NormalizeLineEndings(File.ReadAllText(path));
            if (normalization == GoldenNormalization.SarifAssemblyVersion)
            {
                content = NormalizeSarifVersion(content);
            }

            return content;
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    internal static void WriteGoldenFile(
        string goldenFileName,
        string content,
        GoldenNormalization normalization = GoldenNormalization.None
    )
    {
        string repoRoot = FindRepoRoot();
        string targetDir = Path.Combine(
            repoRoot,
            "Cognitive Code Analysis",
            "CognitiveCodeAnalysis.Tests",
            "fixtures",
            "reports",
            "golden");
        Directory.CreateDirectory(targetDir);

        string normalized = NormalizeLineEndings(content);
        if (normalization == GoldenNormalization.SarifAssemblyVersion)
        {
            normalized = NormalizeSarifVersion(normalized);
        }

        File.WriteAllText(Path.Combine(targetDir, goldenFileName), normalized);
    }

    internal static string NormalizeLineEndings(string content)
        => content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);

    internal static string NormalizeSarifVersion(string content)
        => Regex.Replace(
            content,
            "\"version\"\\s*:\\s*\"[^\"]*\"",
            "\"version\": \"GOLDEN_VERSION\"",
            RegexOptions.CultureInvariant);

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Cognitive Code Analysis", "CognitiveCodeAnalysis.Tests")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root for golden file generation.");
    }
}

public enum GoldenNormalization
{
    None,
    SarifAssemblyVersion,
}
