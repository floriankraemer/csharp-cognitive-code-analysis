/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysisConsoleApp.Infrastructure;
using CognitiveCodeAnalysisConsoleApp.Tests.Reports;

namespace CognitiveCodeAnalysisConsoleApp.Tests.Infrastructure;

internal class SpectreConsoleNotifierTests
{
    private static readonly (string Name, Action<IConsoleNotifier> Call, string Plain)[] Messages =
    [
        ("error", n => n.WriteError("boom [x]"), "Error: boom [x]"),
        ("warning", n => n.WriteWarning("careful [x]"), "Warning: careful [x]"),
        ("no files", n => n.WriteNoSourceFilesFound("/src [x]"), "No C# files found in /src [x]."),
        ("generated", n => n.WriteReportGenerated("Html", "/o [x].html"), "Html report generated: /o [x].html"),
        ("config used", n => n.WriteConfigUsed("defaults [x]"), "Config: defaults [x]"),
        ("config created", n => n.WriteConfigFileCreated("/c [x].json"), "Config file created: /c [x].json"),
        ("init created", n => n.WriteInitConfigCreated("/c [x].json"), "Created config at: /c [x].json"),
    ];

    public static IEnumerable<TestCaseData> Cases()
    {
        foreach (var (name, call, plain) in Messages)
        {
            yield return new TestCaseData(call, plain).SetName(name);
        }
    }

    [TestCaseSource(nameof(Cases))]
    public void NonInteractive_WritesPlainTextToStandardOutput(Action<IConsoleNotifier> call, string plain)
    {
        using var capture = new ConsoleOutputCapture(interactive: false);

        call(new SpectreConsoleNotifier());

        Assert.That(capture.StdOut.Trim(), Is.EqualTo(plain));
        Assert.That(capture.Text, Is.Empty);
    }

    [TestCaseSource(nameof(Cases))]
    public void Interactive_WritesEscapedMarkupThroughTheConsole(Action<IConsoleNotifier> call, string plain)
    {
        using var capture = new ConsoleOutputCapture(interactive: true);

        call(new SpectreConsoleNotifier());

        Assert.That(capture.Text.Replace("\n", string.Empty, StringComparison.Ordinal), Does.Contain("[x]"));
        Assert.That(capture.StdOut, Is.Empty);
        Assert.That(capture.Text.Trim(), Is.EqualTo(plain));
    }
}
