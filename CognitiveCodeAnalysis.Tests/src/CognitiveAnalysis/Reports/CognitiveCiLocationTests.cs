/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

namespace CognitiveCodeAnalysis.Tests.CognitiveAnalysis.Reports;

public class CognitiveCiLocationTests
{
    private const string WorkingDirectory = "/work/project";

    [TestCase("src/A.cs", "src/A.cs")]
    [TestCase("./src/A.cs", "src/A.cs")]
    [TestCase("src//nested/A.cs", "src/nested/A.cs")]
    [TestCase("src/../lib/A.cs", "lib/A.cs")]
    [TestCase("src/My File #1.cs", "src/My File #1.cs")]
    [TestCase("/work/project/src/A.cs", "src/A.cs")]
    [TestCase("/work//project/./src/A.cs", "src/A.cs")]
    [TestCase("src\\A.cs", "src/A.cs")]
    [TestCase("\\work\\project\\src\\A.cs", "src/A.cs")]
    public void DisplayPath_FileBelowWorkingDirectory_IsRelativeWithSlashes(string file, string expected)
    {
        Assert.That(CognitiveCiLocation.DisplayPath(file, WorkingDirectory), Is.EqualTo(expected));
    }

    [TestCase("/tmp/Sample.cs", "/tmp/Sample.cs")]
    [TestCase("../other/A.cs", "/work/other/A.cs")]
    [TestCase("/work/project-old/A.cs", "/work/project-old/A.cs")]
    [TestCase("/work/project", "/work/project")]
    [TestCase("C:\\other\\A.cs", "C:/other/A.cs")]
    [TestCase("C:/work/project/src/A.cs", "C:/work/project/src/A.cs")]
    public void DisplayPath_FileOutsideWorkingDirectory_IsAbsoluteWithSlashes(string file, string expected)
    {
        Assert.That(CognitiveCiLocation.DisplayPath(file, WorkingDirectory), Is.EqualTo(expected));
    }

    [Test]
    public void DisplayPath_WindowsWorkingDirectoryAndWindowsFile_IsRelative()
    {
        Assert.That(
            CognitiveCiLocation.DisplayPath("C:\\work\\project\\src\\A.cs", "C:\\work\\project"),
            Is.EqualTo("src/A.cs"));
    }

    [Test]
    public void DisplayPath_WorkingDirectoryIsRoot_MakesEveryRootedFileRelative()
    {
        Assert.That(CognitiveCiLocation.DisplayPath("/src/A.cs", "/"), Is.EqualTo("src/A.cs"));
    }

    [Test]
    public void DisplayPath_WorkingDirectoryWithTrailingSlash_IsRelative()
    {
        Assert.That(CognitiveCiLocation.DisplayPath("/work/project/src/A.cs", "/work/project/"), Is.EqualTo("src/A.cs"));
    }

    [Test]
    public void DisplayPath_WithoutWorkingDirectory_UsesTheCurrentDirectory()
    {
        string inside = Path.Combine(Directory.GetCurrentDirectory(), "src", "A.cs");

        Assert.That(CognitiveCiLocation.DisplayPath(inside), Is.EqualTo("src/A.cs"));
    }

    [Test]
    public void IsAbsolute_RootedAndDrivePathsAreAbsolute_RelativePathsAreNot()
    {
        Assert.That(CognitiveCiLocation.IsAbsolute("/tmp/A.cs"), Is.True);
        Assert.That(CognitiveCiLocation.IsAbsolute("C:/work/A.cs"), Is.True);
        Assert.That(CognitiveCiLocation.IsAbsolute("src/A.cs"), Is.False);
        Assert.That(CognitiveCiLocation.IsAbsolute("C:"), Is.False);
    }

    [TestCase("/home/a/My File.cs", "file:///home/a/My%20File.cs")]
    [TestCase("/tmp/a#b?c%d.cs", "file:///tmp/a%23b%3Fc%25d.cs")]
    [TestCase("/tmp/ä.cs", "file:///tmp/%C3%A4.cs")]
    [TestCase("C:/work/A.cs", "file:///C:/work/A.cs")]
    [TestCase("C:/my work/A.cs", "file:///C:/my%20work/A.cs")]
    public void FileUri_AbsolutePath_IsPercentEncodedFileUri(string absolute, string expected)
    {
        Assert.That(CognitiveCiLocation.FileUri(absolute), Is.EqualTo(expected));
    }

    [TestCase("/work/my project", "file:///work/my%20project/")]
    [TestCase("/work/project/", "file:///work/project/")]
    [TestCase("/", "file:///")]
    [TestCase("C:\\work\\project", "file:///C:/work/project/")]
    public void WorkingDirectoryUri_EndsWithSlash(string workingDirectory, string expected)
    {
        Assert.That(CognitiveCiLocation.WorkingDirectoryUri(workingDirectory), Is.EqualTo(expected));
    }

    [TestCase("src/a b/ö.cs", "src/a%20b/%C3%B6.cs")]
    [TestCase("a+b/c%d#e?f.cs", "a%2Bb/c%25d%23e%3Ff.cs")]
    public void EncodeUriPath_EncodesEverySegmentButKeepsSeparators(string path, string expected)
    {
        Assert.That(CognitiveCiLocation.EncodeUriPath(path), Is.EqualTo(expected));
    }

    [TestCase("src/a b/ö #1.cs")]
    [TestCase("plain.cs")]
    [TestCase("deep/x%y/z+w.cs")]
    public void SarifUri_RelativePath_ResolvesBackToTheOriginalPathAgainstTheWorkingDirectoryBase(string path)
    {
        var baseUri = new Uri(CognitiveCiLocation.WorkingDirectoryUri("/work/my project"));
        var relativeUri = CognitiveCiLocation.EncodeUriPath(path);

        Uri resolved = new(baseUri, relativeUri);

        Assert.That(Uri.UnescapeDataString(resolved.AbsolutePath), Is.EqualTo("/work/my project/" + path));
    }

    [TestCase("/tmp/a#b?c%d ö.cs")]
    [TestCase("/home/user/My Dir/A+B.cs")]
    public void FileUri_AbsolutePath_ResolvesBackToTheOriginalPath(string path)
    {
        Uri uri = new(CognitiveCiLocation.FileUri(path));

        Assert.That(uri.IsFile, Is.True);
        Assert.That(Uri.UnescapeDataString(uri.AbsolutePath), Is.EqualTo(path));
    }

    [TestCase(0, 1)]
    [TestCase(-3, 1)]
    [TestCase(1, 1)]
    [TestCase(42, 42)]
    public void Line_IsAtLeastOne(int methodLine, int expected)
    {
        var metrics = new CognitiveMetrics("Run", "Svc", "a.cs", "void Run()", methodLine);

        Assert.That(CognitiveCiLocation.Line(metrics), Is.EqualTo(expected));
    }
}
