/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

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

public class SourceFileFinderDiscoveryTests
{
    private TempFiles _temp = null!;
    private readonly SourceFileFinder _finder = new();

    [SetUp]
    public void SetUp() => _temp = new TempFiles();

    [TearDown]
    public void TearDown() => _temp.CleanUp();

    private string Create(string relativePath)
    {
        string path = Path.Combine(_temp.tmpDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "class C { }");
        return path;
    }

    private string Dir(string relativePath) => Path.Combine(_temp.tmpDirectory, relativePath);

    private List<string> Find(params string[] paths) => _finder.FindSourceFiles(paths, [], progress: null);

    [Test]
    public void BuildOutputAndDotDirectories_AreNotSearched()
    {
        string kept = Create("src/A.cs");
        string keptLookalike = Create("src/binary/B.cs");
        Create("src/bin/Debug/Generated.cs");
        Create("src/obj/Release/net8.0/AssemblyInfo.cs");
        Create(".git/hooks/X.cs");
        Create("src/.vs/Y.cs");

        Assert.That(Find(_temp.tmpDirectory), Is.EqualTo(new[] { keptLookalike, kept }.Order(StringComparer.Ordinal)));
    }

    [Test]
    public void ASkippedDirectoryGivenAsSearchPath_IsSearched()
    {
        string generated = Create("obj/Generated.cs");

        Assert.That(Find(Dir("obj")), Is.EqualTo(new[] { generated }));
    }

    [Test]
    public void ASingleSourceFile_IsAccepted()
    {
        string file = Create("src/A.cs");
        Create("src/B.cs");

        Assert.That(Find(file), Is.EqualTo(new[] { file }));
    }

    [Test]
    public void ADirectoryNamedLikeASourceFile_IsSearchedButNotReturned()
    {
        string file = Create("Legacy.cs/A.cs");

        Assert.That(Find(_temp.tmpDirectory), Is.EqualTo(new[] { file }));
    }

    private bool TryLink(string relativeLink, string target)
    {
        try
        {
            Directory.CreateSymbolicLink(Dir(relativeLink), target);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    [Test]
    public void ASymlinkToAnAncestor_IsNotFollowedInALoop()
    {
        string file = Create("src/A.cs");
        if (!TryLink("src/loop", ".."))
        {
            Assert.Ignore("Symbolic links are not supported here.");
        }

        Assert.That(Find(_temp.tmpDirectory), Is.EqualTo(new[] { file }));
    }

    [Test]
    public void ASymlinkToADirectoryOutsideTheSearchPath_IsFollowedOnce()
    {
        var outside = new TempFiles();
        try
        {
            string external = Path.Combine(outside.tmpDirectory, "E.cs");
            File.WriteAllText(external, "class E { }");
            string file = Create("src/A.cs");
            if (!TryLink("src/shared", outside.tmpDirectory) || !TryLink("src/again", outside.tmpDirectory))
            {
                Assert.Ignore("Symbolic links are not supported here.");
            }

            List<string> found = Find(_temp.tmpDirectory);

            Assert.That(found, Has.Count.EqualTo(2));
            Assert.That(found, Does.Contain(file));
        }
        finally
        {
            outside.CleanUp();
        }
    }

    [Test]
    public void AFileThatIsNotCSharp_FindsNothing()
    {
        string file = Path.Combine(_temp.tmpDirectory, "notes.txt");
        File.WriteAllText(file, "text");

        Assert.That(Find(file), Is.Empty);
    }

    [Test]
    public void Results_AreSortedAndWithoutDuplicates()
    {
        string b = Create("b/B.cs");
        string a = Create("a/A.cs");

        Assert.That(Find(Dir("b"), b, _temp.tmpDirectory, Dir("a")), Is.EqualTo(new[] { a, b }));
    }

    [Test]
    public void ExcludeFilePatterns_MatchTheAbsolutePathWithForwardSlashes()
    {
        string kept = Create("src/Service.cs");
        Create("src/Generated/Model.cs");
        Create("src/ServiceTests.cs");

        List<string> found = _finder.FindSourceFiles([_temp.tmpDirectory], ["/Generated/", @"Tests\.cs$"], progress: null);

        Assert.That(found, Is.EqualTo(new[] { kept }));
    }

    [Test]
    public void ExcludeFilePatterns_AreCaseSensitive()
    {
        string kept = Create("src/Service.cs");

        List<string> found = _finder.FindSourceFiles([_temp.tmpDirectory], ["service"], progress: null);

        Assert.That(found, Is.EqualTo(new[] { kept }));
    }

    [Test]
    public void SplitPathList_SplitsTrimsDeduplicatesAndSorts()
    {
        string[] paths = SourceFileFinder.SplitPathList($" {Dir("b")} ,{Dir("a")},, {Dir("b")}");

        Assert.That(paths, Is.EqualTo(new[] { Dir("a"), Dir("b") }));
    }

    [Test]
    public void SplitPathList_MakesRelativePathsAbsolute()
    {
        Assert.That(SourceFileFinder.SplitPathList("src,lib"), Is.EqualTo(new[] { Path.GetFullPath("lib"), Path.GetFullPath("src") }));
    }

    [Test]
    public void SplitPathList_KeepsAnExistingPathWithACommaWhole()
    {
        string withComma = Path.GetDirectoryName(Create("a,b/A.cs"))!;
        Directory.CreateDirectory(Dir("a"));

        Assert.That(SourceFileFinder.SplitPathList(withComma), Is.EqualTo(new[] { withComma }));
    }

    [Test]
    public void SplitPathList_SplitsAMissingPathWithAComma()
    {
        Assert.That(SourceFileFinder.SplitPathList(Dir("x") + "," + Dir("y")), Is.EqualTo(new[] { Dir("x"), Dir("y") }));
    }

    [Test]
    public void SplitPathList_OfOnlySeparators_KeepsTheArgument()
    {
        Assert.That(SourceFileFinder.SplitPathList(" , "), Is.EqualTo(new[] { Path.GetFullPath(" , ") }));
    }
}
