/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.Json;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;
using CognitiveCodeAnalysis.Configuration;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

/// <summary>Paths, line guards and escaping of the CI formats (SARIF, GitLab Code Quality, GitHub Actions).</summary>
public class CiReportPathTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private static CognitiveConfiguration Config() => new() { ScoreThreshold = 5.0, ShowOnlyMethodsExceedingThreshold = false };

    private static string InsideFile => Path.Combine(Directory.GetCurrentDirectory(), "src", "Inside.cs");

    private static CognitiveMetrics Metric(string file, double score, int line = 10, string cls = "Svc") =>
        new("Run", cls, file, "void Run()", line) { totalScore = score };

    private string RenderText(IReport report, CognitiveMetricsCollection collection, string extension)
    {
        string path = Path.Combine(_temp.tmpDirectory, "out." + extension);
        report.RenderMetrics(path, collection, Config());
        return File.ReadAllText(path);
    }

    private JsonElement SarifResult(CognitiveMetrics metrics)
    {
        using JsonDocument doc = JsonDocument.Parse(RenderText(new SarifReport(), new CognitiveMetricsCollection { metrics }, "json"));
        return doc.RootElement.GetProperty("runs")[0].GetProperty("results")[0].Clone();
    }

    private JsonElement SarifRun(CognitiveMetricsCollection collection)
    {
        using JsonDocument doc = JsonDocument.Parse(RenderText(new SarifReport(), collection, "json"));
        return doc.RootElement.GetProperty("runs")[0].Clone();
    }

    [Test]
    public void Sarif_FileBelowWorkingDirectory_IsARelativeUriAgainstTheSourceRoot()
    {
        JsonElement location = SarifResult(Metric(InsideFile, 8)).GetProperty("locations")[0].GetProperty("physicalLocation");

        Assert.That(location.GetProperty("artifactLocation").GetProperty("uri").GetString(), Is.EqualTo("src/Inside.cs"));
        Assert.That(location.GetProperty("artifactLocation").GetProperty("uriBaseId").GetString(), Is.EqualTo("%SRCROOT%"));
    }

    [Test]
    public void Sarif_OriginalUriBaseIds_DeclaresTheWorkingDirectoryAsSourceRoot()
    {
        JsonElement run = SarifRun(new CognitiveMetricsCollection { Metric(InsideFile, 8) });

        string baseUri = run.GetProperty("originalUriBaseIds").GetProperty("%SRCROOT%").GetProperty("uri").GetString()!;

        Assert.That(baseUri, Does.StartWith("file://"));
        Assert.That(baseUri, Does.EndWith("/"));
        Assert.That(
            Path.GetFullPath(new Uri(baseUri).LocalPath).TrimEnd(Path.DirectorySeparatorChar),
            Is.EqualTo(Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar)));
    }

    [Test]
    public void Sarif_FileOutsideWorkingDirectory_IsAFileUriWithoutBase()
    {
        JsonElement artifact = SarifResult(Metric("/tmp/Outside Dir/A.cs", 8))
            .GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation");

        Assert.That(artifact.GetProperty("uri").GetString(), Is.EqualTo("file:///tmp/Outside%20Dir/A.cs"));
        Assert.That(artifact.TryGetProperty("uriBaseId", out _), Is.False);
    }

    [Test]
    public void Sarif_WindowsStylePath_IsNormalizedToAFileUri()
    {
        JsonElement artifact = SarifResult(Metric("C:\\work\\A.cs", 8))
            .GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("artifactLocation");

        Assert.That(artifact.GetProperty("uri").GetString(), Is.EqualTo("file:///C:/work/A.cs"));
    }

    [Test]
    public void Sarif_MethodOnLineZero_StartsAtLineOne()
    {
        JsonElement region = SarifResult(Metric(InsideFile, 8, line: 0))
            .GetProperty("locations")[0].GetProperty("physicalLocation").GetProperty("region");

        Assert.That(region.GetProperty("startLine").GetInt32(), Is.EqualTo(1));
        Assert.That(region.GetProperty("endLine").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public void GitLab_FileBelowWorkingDirectory_IsRelativeWithSlashes()
    {
        var issues = JsonDocument.Parse(RenderText(
            new GitlabCodeQualityReport(),
            new CognitiveMetricsCollection { Metric(InsideFile, 8) },
            "json")).RootElement;

        Assert.That(issues[0].GetProperty("location").GetProperty("path").GetString(), Is.EqualTo("src/Inside.cs"));
    }

    [Test]
    public void GitLab_FileOutsideWorkingDirectory_IsAbsolute()
    {
        var issues = JsonDocument.Parse(RenderText(
            new GitlabCodeQualityReport(),
            new CognitiveMetricsCollection { Metric("/tmp/Outside.cs", 8) },
            "json")).RootElement;

        Assert.That(issues[0].GetProperty("location").GetProperty("path").GetString(), Is.EqualTo("/tmp/Outside.cs"));
    }

    [Test]
    public void GitLab_Fingerprint_HashesTheDisplayedPath()
    {
        var inside = JsonDocument.Parse(RenderText(
            new GitlabCodeQualityReport(),
            new CognitiveMetricsCollection { Metric(InsideFile, 8) },
            "json")).RootElement[0].GetProperty("fingerprint").GetString();
        var relative = JsonDocument.Parse(RenderText(
            new GitlabCodeQualityReport(),
            new CognitiveMetricsCollection { Metric("src/Inside.cs", 8) },
            "json")).RootElement[0].GetProperty("fingerprint").GetString();

        Assert.That(inside, Is.EqualTo(relative));
    }

    [Test]
    public void GitLab_MethodOnLineZero_BeginsAtLineOne()
    {
        var issues = JsonDocument.Parse(RenderText(
            new GitlabCodeQualityReport(),
            new CognitiveMetricsCollection { Metric(InsideFile, 8, line: 0) },
            "json")).RootElement;

        Assert.That(issues[0].GetProperty("location").GetProperty("lines").GetProperty("begin").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public void GithubActions_FileProperty_EscapesColonAndCommaAndPercent()
    {
        string text = RenderText(
            new GithubActionsReport(),
            new CognitiveMetricsCollection { Metric("src/x:y,z 100%.cs", 8) },
            "txt");

        Assert.That(text, Does.StartWith("::warning file=src/x%3Ay%2Cz 100%25.cs,line=10,title="));
    }

    [Test]
    public void GithubActions_MessageWithPercentAndLineBreak_IsEscapedAsData()
    {
        string text = RenderText(
            new GithubActionsReport(),
            new CognitiveMetricsCollection { Metric(InsideFile, 8, cls: "Ops%Co\nX") },
            "txt");

        Assert.That(text, Does.Contain("for Ops%25Co%0AX.Run"));
        Assert.That(text, Does.Not.Contain("Co\nX"));
    }

    [Test]
    public void GithubActions_MethodOnLineZero_AnnotatesLineOne()
    {
        string text = RenderText(
            new GithubActionsReport(),
            new CognitiveMetricsCollection { Metric(InsideFile, 8, line: 0) },
            "txt");

        Assert.That(text, Does.Contain(",line=1,"));
    }

    [Test]
    public void GithubActions_PlainFile_IsNotEscaped()
    {
        string text = RenderText(
            new GithubActionsReport(),
            new CognitiveMetricsCollection { Metric("src/Plain.cs", 8) },
            "txt");

        Assert.That(text, Does.StartWith("::warning file=src/Plain.cs,line=10,"));
    }
}
