namespace CognitiveCodeAnalysis.CognitiveAnalysis.Reports;

internal static class CognitiveReportFileWriter
{
    internal static void Write(string outputFilePath, string content)
    {
        string? directory = Path.GetDirectoryName(outputFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputFilePath, content);
    }
}
