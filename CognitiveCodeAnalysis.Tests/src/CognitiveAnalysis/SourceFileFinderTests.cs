using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class SourceFileFinderTests
{
    private TempFiles _tempFiles;

    [SetUp]
    public void Setup()
    {
        _tempFiles = new TempFiles();
    }

    [TearDown]
    public void TearDown()
    {
        _tempFiles.CleanUp();
    }

    [Test]
    public void TestFindingSourceFiles()
    {
        var file1 = _tempFiles.CreateFileWithContent("File1.cs" , "// Test file 1");
        var file2 = _tempFiles.CreateFileWithContent("File2.cs" , "// Test file 1");

        var fileFinder = new SourceFileFinder();

        // Act
        var result = fileFinder.FindSourceFiles([_tempFiles.tmpDirectory]);

        // Assert
        using (Assert.EnterMultipleScope()) {
            Assert.That(result , Has.Count.EqualTo(2));
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
        Assert.That(result , Has.Count.EqualTo(0));
    }

    [Test]
    public void TestFindingSourceFilesWithValidAndInvalidDirectory()
    {
        // Arrange
        var tempDir1 = _tempFiles.tmpDirectory;
        var file1 = _tempFiles.CreateFileWithContent( "File1.cs", "// Test file 1");

        File.WriteAllText(file1 , "// Test file 1");

        var fileFinder = new SourceFileFinder();

        // Act
        var result = fileFinder.FindSourceFiles([
            "/does-not-exist",
            tempDir1
        ]);

        // Assert
        Assert.That(result , Has.Count.EqualTo(1));
    }
}
