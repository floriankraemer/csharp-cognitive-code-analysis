namespace CognitiveCodeAnalysis;

public class FileFinder
{
    public List<string> Find(string[] directories)
    {
        var files = new List<string>();

        foreach (string directory in directories)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                continue;
            }

            // Normalize the path - remove surrounding quotes and whitespace, then get full path
            string normalizedDirectory = directory.Trim().Trim('"', '\'');
            
            try
            {
                // Get the full absolute path - this will normalize the path properly
                normalizedDirectory = Path.GetFullPath(normalizedDirectory);
            }
            catch
            {
                // If path is invalid, skip it
                continue;
            }
            
            if (!Directory.Exists(normalizedDirectory))
            {
                continue;
            }

            try
            {
                var foundFiles = Directory.GetFiles(normalizedDirectory, "*.cs", SearchOption.AllDirectories);
                files.AddRange(foundFiles);
            }
            catch
            {
                // If we can't access the directory, skip it
                continue;
            }
        }

        return files;
    }
}
