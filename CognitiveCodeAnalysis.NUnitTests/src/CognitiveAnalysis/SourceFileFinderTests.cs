using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class SourceFileFinderTests
{
    private readonly List<string> _tempDirectories = [];

    [SetUp]
    public void Setup()
    {
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var dir in _tempDirectories) {
            try {
                if (Directory.Exists(dir)) {
                    Directory.Delete(dir , recursive: true);
                }
            } catch {
                // Ignore cleanup errors
            }
        }
    }

    private string CreateTempDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath() , Guid.NewGuid().ToString());

        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);

        return tempDir;
    }

    [Test]
    public void TestFindingSourceFiles()
    {
        // Arrange
        var tempDir1 = CreateTempDirectory();
        var tempDir2 = CreateTempDirectory();
        var file1 = Path.Combine(tempDir1 , "File1.cs");
        var file2 = Path.Combine(tempDir2 , "File2.cs");

        File.WriteAllText(file1 , "// Test file 1");
        File.WriteAllText(file2 , "// Test file 2");

        var fileFinder = new SourceFileFinder();

        // Act
        var result = fileFinder.FindSourceFiles([tempDir1 , tempDir2]);

        // Assert
        using (Assert.EnterMultipleScope()) {
            Assert.That(2 , Is.EqualTo(result.Count));
            Assert.That(result, Contains.Item(file1));
            Assert.That(result , Contains.Item(file2));
        }
    }

    [Test]
    public void TestFindingSourceFilesFromInvalidDirectory()
    {
        // Arrange
        var fileFinder = new SourceFileFinder();

        // Act
        var result = fileFinder.FindSourceFiles([
            "/does-not-exist"
        ]);

        // Assert
        Assert.That(0 , Is.EqualTo(result.Count));
    }

    [Test]
    public void TestFindingSourceFilesWithValidAndInvalidDirectory()
    {
        // Arrange
        var tempDir1 = CreateTempDirectory();
        var file1 = Path.Combine(tempDir1 , "File1.cs");

        File.WriteAllText(file1 , "// Test file 1");

        var fileFinder = new SourceFileFinder();
        var noneExistentDirectory = "/does-not-exist";

        // Act
        var result = fileFinder.FindSourceFiles([
            "/does-not-exist",
            tempDir1
        ]);

        // Assert
        Assert.That(1 , Is.EqualTo(result.Count));
    }
}
