namespace CognitiveCodeAnalysis
{
    public class FileFinder
    {
        public List<string> Find(string[] directories)
        {
            var files = new List<string>();

            foreach (string directory in directories)
            {
                if (!Directory.Exists(directory))
                {
                    continue;
                }

                files.AddRange(Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories));
            }

            return files;
        }
    }
}
