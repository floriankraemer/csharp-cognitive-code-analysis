namespace CognitiveCodeAnalysis.Tests;

public class TempFiles
{
    public readonly string tmpDirectory;

    public void CleanUp()
    {
        if (Directory.Exists(tmpDirectory)) {
            Directory.Delete(tmpDirectory , recursive: true);
        }
    }

    public TempFiles()
    {
        tmpDirectory = Path.Combine(Path.GetTempPath() , Guid.NewGuid().ToString());

        Directory.CreateDirectory(tmpDirectory);
    }

    public string CreateFile(string filename)
    {
        return CreateFileWithContent(filename , string.Empty);
    }

    public string CreateFileWithContent(string filename, string fileContent)
    {
        var file = Path.Combine(tmpDirectory , filename);

        File.WriteAllText(file , fileContent);

        return file;
    }
}
