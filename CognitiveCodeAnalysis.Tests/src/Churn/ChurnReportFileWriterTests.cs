/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Churn;

namespace CognitiveCodeAnalysis.Tests.Churn;

public class ChurnReportFileWriterTests
{
    private TempFiles _temp = null!;

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    [Test]
    public void Write_ReplacesAnExistingFileAndLeavesNoTemporaryFileBehind()
    {
        string file = _temp.CreateFileWithContent("report.json", "old");

        ChurnReportFileWriter.Write(file, "new");

        Assert.That(File.ReadAllText(file), Is.EqualTo("new"));
        Assert.That(Directory.GetFileSystemEntries(_temp.tmpDirectory), Has.Length.EqualTo(1));
    }

    [Test]
    public void Write_FailureKeepsTheExistingFileAndLeavesNoTemporaryFileBehind()
    {
        // The target is a directory, so the final move fails after the temporary file was written.
        string target = Path.Combine(_temp.tmpDirectory, "report.json");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "keep.txt"), "x");

        Assert.Catch<Exception>(() => ChurnReportFileWriter.Write(target, "new"));

        Assert.That(File.Exists(Path.Combine(target, "keep.txt")), Is.True);
        Assert.That(Directory.GetFileSystemEntries(_temp.tmpDirectory), Has.Length.EqualTo(1));
    }
}
