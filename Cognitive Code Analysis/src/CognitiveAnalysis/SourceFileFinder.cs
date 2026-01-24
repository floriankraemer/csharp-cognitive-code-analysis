namespace CognitiveCodeAnalysis.CognitiveAnalysis;

/// <summary>
/// <![CDATA[It will recursivly scan and return a list of csharp source files]]>
/// </summary>
public class SourceFileFinder
{
    /// <summary>
    /// <![CDATA[Takes a list of directories it will recursivly scan and return a list of CSharp Files]]>
    /// </summary>
    /// <param name="directories"></param>
    /// <returns><![CDATA[A list of CSharp Files]]></returns>
    public List<string> FindSourceFiles(string[] directories)
    {
        var files = new List<string>();

        foreach (string directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            string normalizedDirectory = NormalizePath(directory);

            if (!Directory.Exists(normalizedDirectory))
            {
                continue;
            }

            files.AddRange(GetFilesFromDirectory(normalizedDirectory, files));
        }

        return files;
    }

    private static string NormalizePath(string directory)
    {
        return Path.GetFullPath(directory.Trim().Trim('"', '\''));
    }

    private static string[] GetFilesFromDirectory(string normalizedDirectory, List<string> files)
    {
        string[] foundFiles = Directory.GetFiles(
            path: normalizedDirectory,
            searchPattern: "*.cs",
            searchOption: SearchOption.AllDirectories
        );

        return foundFiles;
    }
}
