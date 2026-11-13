/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace CognitiveCodeAnalysis.Configuration;

/// <summary>
/// Creates a configuration file: the built-in defaults with the answers applied, written without overwriting an
/// existing file unless asked to.
/// </summary>
public static class ConfigInitializer
{
    private const string RootSection = "cognitive";
    private static readonly JsonSerializerOptions IndentedOptions = new() { WriteIndented = true };
    private static readonly Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    // System.Text.Json on .NET 8 indents by two spaces and cannot change it. Each level is exactly two spaces and a JSON
    // string never holds a raw line break, so doubling the leading spaces of every line gives the four-space layout of
    // the shipped file.
    private static readonly Regex LeadingSpaces = new("^ +", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Renders the shipped settings file with the answers applied. Keys and their order stay as shipped.
    /// </summary>
    public static string Render(ConfigInitAnswers answers)
    {
        JsonObject root = JsonNode.Parse(CognitiveConfigurationDefaults.SettingsJson)!.AsObject();
        JsonObject cognitive = root[RootSection]!.AsObject();

        cognitive["scoreThreshold"] = answers.ScoreThreshold;
        cognitive["showOnlyMethodsExceedingThreshold"] = answers.ShowOnlyMethodsExceedingThreshold;
        cognitive["showHalsteadComplexity"] = answers.ShowHalsteadComplexity;
        cognitive["showCyclomaticComplexity"] = answers.ShowCyclomaticComplexity;
        cognitive["showCouplingMetrics"] = answers.ShowCouplingMetrics;
        cognitive["showDetailedCognitiveMetrics"] = answers.ShowDetailedCognitiveMetrics;
        cognitive["groupByClass"] = answers.GroupByClass;

        string indented = root.ToJsonString(IndentedOptions);
        return LeadingSpaces.Replace(indented, match => match.Value + match.Value) + "\n";
    }

    /// <summary>
    /// Checks the target before any question is asked, so a refused target is reported without wasting answers.
    /// </summary>
    /// <returns>The full path of the file to create.</returns>
    /// <exception cref="ConfigFileException">The path is empty or a directory, or the file exists and
    /// <paramref name="overwrite"/> is false.</exception>
    public static string PrepareTarget(string path, bool overwrite)
    {
        string fullPath = ResolvePath(path);

        if (!overwrite && File.Exists(fullPath))
        {
            throw AlreadyExists(fullPath);
        }

        return fullPath;
    }

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="fullPath"/>, creating missing parent directories.
    /// The content goes to a temporary file first and is then moved into place, so the target never holds partial
    /// content. Without <paramref name="overwrite"/> the move refuses to replace an existing file.
    /// </summary>
    /// <exception cref="ConfigFileException">The file cannot be written, or it exists and
    /// <paramref name="overwrite"/> is false.</exception>
    public static void Write(string fullPath, string content, bool overwrite)
    {
        string directory = Path.GetDirectoryName(fullPath)!;
        string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(temporaryPath, content, FileEncoding);
            File.Move(temporaryPath, fullPath, overwrite);
        }
        catch (IOException) when (!overwrite && File.Exists(fullPath))
        {
            throw AlreadyExists(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new ConfigFileException($"Cannot write config file {fullPath}: {exception.Message}", exception);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ConfigFileException("Invalid path: path must not be empty.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (ArgumentException exception)
        {
            throw new ConfigFileException($"Invalid path {path}: {exception.Message}", exception);
        }

        if (Directory.Exists(fullPath))
        {
            throw new ConfigFileException($"Cannot write config file {path}: it is a directory.");
        }

        return fullPath;
    }

    private static ConfigFileException AlreadyExists(string path) =>
        new($"Config file already exists at {path}. Use --force to overwrite.");
}
