namespace CognitiveCodeAnalysis.CognitiveAnalysis;

public class FileFinder
{
    private static string NormalizePath(string directory)
    {
        return Path.GetFullPath(directory.Trim().Trim('"', '\''));
    }

    public List<string> Find(string[] directories)
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

            string[] foundFiles = Directory.GetFiles(normalizedDirectory, "*.cs", SearchOption.AllDirectories);
            files.AddRange(foundFiles);
        }

        return files;
    }
}
