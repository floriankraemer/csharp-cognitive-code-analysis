using CognitiveCodeAnalysis.CognitiveAnalysis;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis;

public class FileFinderTests : IDisposable
{
    private readonly List<string> _tempDirectories = new();

    [Fact]
    public void Find_WithValidDirectory_ReturnsCSharpFiles()
    {
        // Arrange
        string tempDir = CreateTempDirectory();
        string file1 = Path.Combine(tempDir, "Test1.cs");
        string file2 = Path.Combine(tempDir, "Test2.cs");
        File.WriteAllText(file1, "// Test file 1");
        File.WriteAllText(file2, "// Test file 2");

        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { tempDir });

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(file1, result);
        Assert.Contains(file2, result);
    }

    [Fact]
    public void Find_WithMultipleDirectories_ReturnsFilesFromAll()
    {
        // Arrange
        string tempDir1 = CreateTempDirectory();
        string tempDir2 = CreateTempDirectory();
        string file1 = Path.Combine(tempDir1, "File1.cs");
        string file2 = Path.Combine(tempDir2, "File2.cs");
        File.WriteAllText(file1, "// Test file 1");
        File.WriteAllText(file2, "// Test file 2");

        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { tempDir1, tempDir2 });

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(file1, result);
        Assert.Contains(file2, result);
    }

    [Fact]
    public void Find_WithSubdirectories_ReturnsFilesRecursively()
    {
        // Arrange
        string tempDir = CreateTempDirectory();
        string subDir = Path.Combine(tempDir, "SubDir");
        Directory.CreateDirectory(subDir);
        string file1 = Path.Combine(tempDir, "File1.cs");
        string file2 = Path.Combine(subDir, "File2.cs");
        File.WriteAllText(file1, "// Test file 1");
        File.WriteAllText(file2, "// Test file 2");

        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { tempDir });

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(file1, result);
        Assert.Contains(file2, result);
    }

    [Fact]
    public void Find_WithNullDirectory_SkipsNull()
    {
        // Arrange
        string tempDir = CreateTempDirectory();
        string file1 = Path.Combine(tempDir, "File1.cs");
        File.WriteAllText(file1, "// Test file");

        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { null!, tempDir });

        // Assert
        Assert.Single(result);
        Assert.Contains(file1, result);
    }

    [Fact]
    public void Find_WithEmptyString_SkipsEmpty()
    {
        // Arrange
        string tempDir = CreateTempDirectory();
        string file1 = Path.Combine(tempDir, "File1.cs");
        File.WriteAllText(file1, "// Test file");

        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { "", tempDir });

        // Assert
        Assert.Single(result);
        Assert.Contains(file1, result);
    }

    [Fact]
    public void Find_WithWhitespace_SkipsWhitespace()
    {
        // Arrange
        string tempDir = CreateTempDirectory();
        string file1 = Path.Combine(tempDir, "File1.cs");
        File.WriteAllText(file1, "// Test file");

        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { "   ", tempDir });

        // Assert
        Assert.Single(result);
        Assert.Contains(file1, result);
    }

    [Fact]
    public void Find_WithQuotedPath_RemovesQuotes()
    {
        // Arrange
        string tempDir = CreateTempDirectory();
        string file1 = Path.Combine(tempDir, "File1.cs");
        File.WriteAllText(file1, "// Test file");

        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { $"\"{tempDir}\"" });

        // Assert
        Assert.Single(result);
        Assert.Contains(file1, result);
    }

    [Fact]
    public void Find_WithSingleQuotedPath_RemovesQuotes()
    {
        // Arrange
        string tempDir = CreateTempDirectory();
        string file1 = Path.Combine(tempDir, "File1.cs");
        File.WriteAllText(file1, "// Test file");

        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { $"'{tempDir}'" });

        // Assert
        Assert.Single(result);
        Assert.Contains(file1, result);
    }

    [Fact]
    public void Find_WithPathWithWhitespace_TrimsWhitespace()
    {
        // Arrange
        string tempDir = CreateTempDirectory();
        string file1 = Path.Combine(tempDir, "File1.cs");
        File.WriteAllText(file1, "// Test file");

        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { $"   {tempDir}   " });

        // Assert
        Assert.Single(result);
        Assert.Contains(file1, result);
    }

    [Fact]
    public void Find_WithNonExistentDirectory_SkipsDirectory()
    {
        // Arrange
        string nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { nonExistentDir });

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Find_WithInvalidPath_SkipsInvalidPath()
    {
        // Arrange
        var fileFinder = new FileFinder();
        string invalidPath = "|<>?*";

        // Act
        var result = fileFinder.Find(new[] { invalidPath });

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Find_WithEmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(Array.Empty<string>());

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void Find_OnlyReturnsCSharpFiles()
    {
        // Arrange
        string tempDir = CreateTempDirectory();
        string csFile = Path.Combine(tempDir, "Test.cs");
        string txtFile = Path.Combine(tempDir, "Test.txt");
        string jsFile = Path.Combine(tempDir, "Test.js");
        File.WriteAllText(csFile, "// C# file");
        File.WriteAllText(txtFile, "Text file");
        File.WriteAllText(jsFile, "// JS file");

        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { tempDir });

        // Assert
        Assert.Single(result);
        Assert.Contains(csFile, result);
        Assert.DoesNotContain(txtFile, result);
        Assert.DoesNotContain(jsFile, result);
    }

    [Fact]
    public void Find_WithMixedValidAndInvalidDirectories_ReturnsFilesFromValidOnly()
    {
        // Arrange
        string tempDir = CreateTempDirectory();
        string file1 = Path.Combine(tempDir, "File1.cs");
        File.WriteAllText(file1, "// Test file");
        string nonExistentDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        var fileFinder = new FileFinder();

        // Act
        var result = fileFinder.Find(new[] { tempDir, nonExistentDir, null!, "" });

        // Assert
        Assert.Single(result);
        Assert.Contains(file1, result);
    }

    private string CreateTempDirectory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        _tempDirectories.Add(tempDir);
        return tempDir;
    }

    public void Dispose()
    {
        foreach (string dir in _tempDirectories)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
