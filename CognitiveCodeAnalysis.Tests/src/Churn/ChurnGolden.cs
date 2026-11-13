/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Tests.Churn;

/// <summary>
/// Compares output with a committed golden file. On a mismatch the actual output is written next to the golden file
/// as <c>*.actual</c> (in the build output), to be reviewed and copied over to <c>fixtures/churn</c>.
/// </summary>
internal static class ChurnGolden
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "fixtures", "churn");

    internal static string Read(string name) => File.ReadAllText(Path.Combine(Root, name));

    internal static void AssertEqual(string name, string actual)
    {
        string path = Path.Combine(Root, name);
        string expected = File.Exists(path) ? File.ReadAllText(path) : "<missing golden file>";
        if (expected != actual)
        {
            File.WriteAllText(path + ".actual", actual);
        }

        Assert.That(actual, Is.EqualTo(expected), $"Golden file {name} differs; see {path}.actual");
    }
}
