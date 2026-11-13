/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

using CognitiveCodeAnalysisConsoleApp.Tests.Reports;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Application;

/// <summary>Drives <c>--sort-by</c>, <c>--sort-order</c>, <c>--debug</c> and <c>--version</c> through the real composition root.</summary>
public class SortAndVersionEndToEndTests
{
    private string _dir = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"cca-sort-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(
            Path.Combine(_dir, "Sample.cs"),
            """
            namespace Samples;

            public class Svc
            {
                public int Mid(int a) { if (a > 0) { return 1; } return 0; }
                public int Low(int a) { return a; }
                public int High(int a, int b)
                {
                    if (a > 0) { if (b > 0) { foreach (var i in new[] { a, b }) { if (i > 1) { return i; } } } }
                    return 0;
                }
            }

            public class Other
            {
                public int First(int a) { return a + 1; }
            }
            """);
        File.WriteAllText(P("grouped.json"), Config(grouped: true));
        File.WriteAllText(P("ungrouped.json"), Config(grouped: false));
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    private string P(string name) => Path.Combine(_dir, name);

    private static string Config(bool grouped) =>
        $$"""{ "cognitive": { "showOnlyMethodsExceedingThreshold": false, "groupByClass": {{(grouped ? "true" : "false")}} } }""";

    private (int ExitCode, ConsoleOutputCapture Output) Run(params string[] args)
    {
        var capture = new ConsoleOutputCapture();
        return (Program.Main(args), capture);
    }

    private static string[] Methods(string text, string pattern) =>
        Regex.Matches(text, pattern, RegexOptions.Multiline).Select(m => m.Groups[1].Value).ToArray();

    private string[] Console_(bool grouped, params string[] sort)
    {
        var (exitCode, output) = Run([_dir, "-c", P(grouped ? "grouped.json" : "ungrouped.json"), .. sort]);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0), output.Text + output.StdOut);
            return Methods(output.Text, @"L\d+ (\w+)");
        }
    }

    private string File_(string format, string extension, params string[] sort)
    {
        string path = P("out." + extension);
        var (exitCode, output) = Run([_dir, "-c", P("ungrouped.json"), "-f", format, "-o", path, .. sort]);
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0), output.Text + output.StdOut);
        }

        return File.ReadAllText(path);
    }

    // ---- sorting ----

    [Test]
    public void Console_Ungrouped_WithoutSort_OrdersByScoreDescending()
    {
        string[] order = Console_(grouped: false);

        Assert.That(order.First(), Is.EqualTo("High"));
        Assert.That(order, Is.EquivalentTo(new[] { "High", "Mid", "Low", "First" }));
    }

    [Test]
    public void Console_Ungrouped_SortByMethod_IsGlobal()
        => Assert.That(Console_(false, "--sort-by", "method"), Is.EqualTo(new[] { "First", "High", "Low", "Mid" }));

    [Test]
    public void Console_Ungrouped_SortByMethodDescending()
        => Assert.That(Console_(false, "--sort-by", "METHOD", "--sort-order", "DESC"), Is.EqualTo(new[] { "Mid", "Low", "High", "First" }));

    [Test]
    public void Console_Grouped_SortByMethod_SortsWithinEachClassTable()
    {
        string[] order = Console_(true, "--sort-by", "method");

        // Classes stay ordered by name; the rows inside Svc follow the sort.
        Assert.That(order, Is.EqualTo(new[] { "First", "High", "Low", "Mid" }));
    }

    [Test]
    public void Console_Grouped_SortByLineDescending_SortsWithinEachClassTable()
        => Assert.That(Console_(true, "--sort-by", "line", "--sort-order", "desc"), Is.EqualTo(new[] { "First", "High", "Low", "Mid" }));

    [Test]
    public void Console_Grouped_WithoutSort_KeepsSourceOrderWithinClass()
        => Assert.That(Console_(true), Is.EqualTo(new[] { "First", "Mid", "Low", "High" }));

    [Test]
    public void Console_Grouped_SortByScore_PutsTheMostComplexLast()
        => Assert.That(Console_(true, "--sort-by", "score").Last(), Is.EqualTo("High"));

    [Test]
    public void Csv_SortByMethod_FollowsTheSort()
    {
        string[] rows = File_("Csv", "csv", "--sort-by", "method").Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();

        Assert.That(rows.Select(r => r.Split(',')[2]), Is.EqualTo(new[] { "First", "High", "Low", "Mid" }));
    }

    [Test]
    public void Csv_WithoutSort_OrdersByScoreDescending()
    {
        string[] rows = File_("Csv", "csv").Split('\n', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();

        Assert.That(rows[0].Split(',')[2], Is.EqualTo("High"));
    }

    [Test]
    public void Html_SortByMethodDescending_FollowsTheSort()
        => Assert.That(Methods(File_("Html", "html", "--sort-by", "method", "--sort-order", "desc"), @"L\d+ (\w+)"), Is.EqualTo(new[] { "Mid", "Low", "High", "First" }));

    [Test]
    public void Markdown_SortByMethod_FollowsTheSort()
        => Assert.That(Methods(File_("Markdown", "md", "--sort-by", "method"), @"^\| L\d+ (\w+)"), Is.EqualTo(new[] { "First", "High", "Low", "Mid" }));

    [Test]
    public void Json_SortByLine_FollowsTheSort()
    {
        using var document = JsonDocument.Parse(File_("Json", "json", "--sort-by", "line"));

        var names = document.RootElement.GetProperty("methods").EnumerateArray().Select(m => m.GetProperty("methodName").GetString());

        Assert.That(names, Is.EqualTo(new[] { "Mid", "Low", "High", "First" }));
    }

    [Test]
    public void Json_WithoutSort_KeepsTheSnapshotOrder()
    {
        using var document = JsonDocument.Parse(File_("Json", "json"));

        var names = document.RootElement.GetProperty("methods").EnumerateArray().Select(m => m.GetProperty("methodName").GetString());

        // The snapshot is ordered by file, class and signature.
        Assert.That(names, Is.EqualTo(new[] { "First", "High", "Low", "Mid" }));
    }

    [TestCase("--sort-by", "bogus")]
    [TestCase("--sort-by", "")]
    public void InvalidField_FailsAndListsTheValidFields(string flag, string value)
    {
        var (exitCode, output) = Run(_dir, flag, value);
        using (output)
        {
            Assert.That(exitCode, Is.Not.EqualTo(0));
            Assert.That(output.Text + output.StdOut, Does.Contain("Valid fields: score, class, method, file, line"));
        }
    }

    [Test]
    public void InvalidOrder_FailsAndListsTheValidValues()
    {
        var (exitCode, output) = Run(_dir, "--sort-by", "score", "--sort-order", "sideways");
        using (output)
        {
            Assert.That(exitCode, Is.Not.EqualTo(0));
            Assert.That(output.Text + output.StdOut, Does.Contain("Invalid sort order \"sideways\". Valid values: asc, desc."));
        }
    }

    [Test]
    public void InvalidSort_WritesNoReport()
    {
        var (exitCode, output) = Run(_dir, "--sort-by", "bogus", "-f", "Csv", "-o", P("never.csv"));
        output.Dispose();

        Assert.That(exitCode, Is.Not.EqualTo(0));
        Assert.That(File.Exists(P("never.csv")), Is.False);
    }

    // ---- debug ----

    [Test]
    public void Debug_PrintsTheSummaryToStandardError_AndLeavesTheReportAlone()
    {
        var (exitCode, output) = Run(_dir, "--debug", "-c", P("ungrouped.json"));
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output.StdErr, Does.Match(@"^Execution Summary:\n  Total execution time: \d+\.\d{3}s\nPhase timings:\n"));
            Assert.That(output.StdErr, Does.Contain("    SearchingFiles: "));
            Assert.That(output.StdErr, Does.Contain("    AnalysingFiles: "));
            Assert.That(output.StdErr, Does.Contain("Files found: 1\n"));
            Assert.That(output.StdErr, Does.Contain("Methods analysed: 4\n"));
            Assert.That(output.Text + output.StdOut, Does.Not.Contain("Execution Summary"));
        }
    }

    [Test]
    public void WithoutDebug_NothingIsPrintedToStandardError()
    {
        var (_, output) = Run(_dir, "-c", P("ungrouped.json"));
        using (output)
        {
            Assert.That(output.StdErr, Is.Empty);
        }
    }

    [Test]
    public void Verbose_IsUnchangedByDebug()
    {
        var (_, output) = Run(_dir, "-v", "-c", P("ungrouped.json"));
        using (output)
        {
            Assert.That(output.StdErr, Does.Contain("Run started (report=ConsoleText"));
            Assert.That(output.StdErr, Does.Not.Contain("Execution Summary"));
        }
    }

    // ---- version ----

    [Test]
    public void Version_PrintsTheInformationalVersionWithoutTheShaSuffix()
    {
        string informational = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        string expected = informational.Split('+')[0];

        var (exitCode, output) = Run("--version");
        using (output)
        {
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That((output.Text + output.StdOut).Trim(), Is.EqualTo(expected));
            Assert.That(expected, Does.Not.Contain("+"));
        }
    }

    [Test]
    public void Version_Property_StripsTheSuffix()
        => Assert.That(Program.Version, Does.Match(@"^\d+\.\d+\.\d+[^+]*$"));
}
