/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text.Json;

using CognitiveCodeAnalysisConsoleApp.Tests.Reports;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Application;

/// <summary>
/// Which files and declarations end up in a report, driven through <see cref="Program.Main"/>:
/// the search path forms, the skipped build output, <c>@cca-ignore</c> and the exclude patterns of the configuration.
/// </summary>
public class SourceSelectionEndToEndTests
{
    private string _dir = null!;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"cca-selection-{Guid.NewGuid():N}");
        Write("src/Shapes.cs", """
            namespace Shapes;

            public record Circle(double Radius)
            {
                public Circle(double radius, bool clamp) : this(clamp && radius < 0 ? 0 : radius) { }

                public double Area() => System.Math.PI * Radius * Radius;
            }

            public struct Point
            {
                public Point(int x) { X = x; }
                public int X { get; }
                public int Twice() => X * 2;
            }

            public interface IShape
            {
                double Area();
                string Describe() => Area() > 1 ? "large" : "small";
            }

            /// <summary>Generated.</summary>
            /// <remarks>@cca-ignore</remarks>
            public class Generated
            {
                public void Hidden() { }
            }

            public class Legacy
            {
                // @cca-ignore
                public void Hidden() { }

                public void GetValue() { }

                public void Kept() { }
            }
            """);
        Write("lib/Util.cs", "namespace Lib; public class Util { public void Help() { } }");
        Write("src/bin/Debug/Gen.cs", "public class BinGen { public void Hidden() { } }");
        Write("src/obj/Gen.cs", "public class ObjGen { public void Hidden() { } }");
        Write("src/.hidden/Gen.cs", "public class DotGen { public void Hidden() { } }");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_dir, recursive: true);

    private string Path_(string relativePath) => Path.Combine(_dir, relativePath);

    private void Write(string relativePath, string content)
    {
        string path = Path_(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private (int ExitCode, string Output, string[] Units) Analyse(params string[] arguments)
    {
        string report = Path_($"report-{Guid.NewGuid():N}.json");
        using var capture = new ConsoleOutputCapture(interactive: false);
        int exitCode = Program.Main([.. arguments, "-f", "Json", "-o", report]);
        string output = capture.Text + capture.StdOut;

        if (!File.Exists(report))
        {
            return (exitCode, output, []);
        }

        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(report));
        string[] units = json.RootElement.GetProperty("methods").EnumerateArray()
            .Select(m => $"{m.GetProperty("className").GetString()}::{m.GetProperty("methodName").GetString()}")
            .ToArray();
        return (exitCode, output, units);
    }

    private static readonly string[] ShapesUnits =
    [
        "Shapes.Circle::Circle",
        "Shapes.Circle::Area",
        "Shapes.IShape::Describe",
        "Shapes.Legacy::GetValue",
        "Shapes.Legacy::Kept",
        "Shapes.Point::Point",
        "Shapes.Point::Twice",
    ];

    [Test]
    public void Directory_ReportsRecordsStructsInterfacesAndConstructors_AndSkipsIgnoredAndBuildOutput()
    {
        var (exitCode, _, units) = Analyse(Path_("src"));

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(units, Is.EqualTo(ShapesUnits));
    }

    [Test]
    public void SingleFile_IsAnalysed()
    {
        var (exitCode, _, units) = Analyse(Path_("lib/Util.cs"));

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(units, Is.EqualTo(new[] { "Lib.Util::Help" }));
    }

    [Test]
    public void CommaSeparatedList_IsTrimmedAndDeduplicated()
    {
        var (exitCode, _, units) = Analyse($"{Path_("src")} , {Path_("lib/Util.cs")},{Path_("lib")}");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(units, Is.EqualTo(new[] { "Lib.Util::Help" }.Concat(ShapesUnits)));
    }

    [Test]
    public void DirectoryWithACommaInItsName_IsOnePath()
    {
        Write("a,b/One.cs", "public class One { public void M() { } }");

        var (exitCode, _, units) = Analyse(Path_("a,b"));

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(units, Is.EqualTo(new[] { "One::M" }));
    }

    [Test]
    public void MissingPath_ReportsNoSourceFilesAndFails()
    {
        string missing = Path_("missing");

        var (exitCode, output, _) = Analyse(missing);

        Assert.That(exitCode, Is.EqualTo(-1));
        Assert.That(output, Does.Contain("No C# files found in " + missing));
    }

    [Test]
    public void ExcludePatternsFromTheConfiguration_AreApplied()
    {
        Write("settings.json", """
            {
              "cognitive": {
                "excludeFilePatterns": ["/lib/"],
                "excludePatterns": ["::Get", "^Shapes\\.(Point|Circle)::"]
              }
            }
            """);

        var (exitCode, output, units) = Analyse($"{Path_("src")},{Path_("lib")}", "-c", Path_("settings.json"));

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(units, Is.EqualTo(new[] { "Shapes.IShape::Describe", "Shapes.Legacy::Kept" }));
        Assert.That(output, Does.Not.Contain("Warning"));
    }

    [Test]
    public void InvalidExcludePattern_IsIgnoredWithAWarning()
    {
        Write("settings.json", """{ "cognitive": { "excludePatterns": ["(", "::Get"] } }""");

        var (exitCode, output, units) = Analyse(Path_("src"), "-c", Path_("settings.json"));

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Contain("Invalid regular expression '(' in 'excludePatterns' is ignored"));
        Assert.That(units, Does.Not.Contain("Shapes.Legacy::GetValue"));
        Assert.That(units, Does.Contain("Shapes.Legacy::Kept"));
    }
}
