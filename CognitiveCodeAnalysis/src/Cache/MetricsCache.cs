/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Common;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.HalsteadAnalysis;
using CognitiveCodeAnalysis.UnderstandabilityAnalysis;

using Microsoft.CodeAnalysis;

namespace CognitiveCodeAnalysis.Cache;

/// <summary>
/// Stores the unscored metrics of each source file so a run on an unchanged tree does not analyse it again.
/// <para>
/// The metrics of a file depend on the other files: the field and property counts come from the semantic model of
/// one compilation of all files, so a member that changes from a field to a property in file B changes the result
/// of file A. An entry is therefore keyed by the digest of the content of every file of the run. Any change to any
/// file invalidates every entry. That is the ceiling: the cache pays off for repeated runs on the same tree
/// (several report types in one CI job), not for incremental runs after an edit.
/// </para>
/// <para>
/// An entry is valid for one file, one run digest, one analysis configuration, one build of the tool and one runtime.
/// Entries live at <c>&lt;directory&gt;/&lt;key[0..2]&gt;/&lt;key&gt;.json</c>. A disabled cache never touches the file system.
/// Coupling is not cached: it is computed across all files and is cheap next to the per-method metrics.
/// </para>
/// </summary>
public sealed class MetricsCache
{
    /// <summary>Bump when the payload changes, so entries of an older layout are read as misses.</summary>
    internal const int FormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    private static readonly string DefaultToolFingerprint =
        $"{typeof(MetricsCache).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion}|{RuntimeInformation.FrameworkDescription}";

    private readonly CacheSettings _settings;
    private readonly string _directory;
    private readonly Action<string>? _warn;
    private readonly string _toolFingerprint;
    private int _writeFailureReported;

    public MetricsCache(CacheSettings settings, string workingDirectory, Action<string>? warn = null)
        : this(settings, workingDirectory, warn, DefaultToolFingerprint)
    {
    }

    internal MetricsCache(CacheSettings settings, string workingDirectory, Action<string>? warn, string toolFingerprint)
    {
        _toolFingerprint = toolFingerprint;
        _settings = settings;
        string configured = string.IsNullOrWhiteSpace(settings.Directory) ? CacheSettings.DefaultDirectory : settings.Directory;
        _directory = Path.GetFullPath(configured, workingDirectory);
        _warn = warn;
    }

    public bool Enabled => _settings.Enabled;

    /// <returns>The cache of one run, or null when the cache is disabled.</returns>
    public Run? Begin(CompiledSourceSet sources, CognitiveConfiguration configuration)
    {
        if (!Enabled)
        {
            return null;
        }

        var contentHashes = new Dictionary<SyntaxTree, string>();
        foreach (SyntaxTree tree in sources.SyntaxTrees)
        {
            contentHashes[tree] = Sha256(tree.GetText().ToString());
        }

        string runDigest = Sha256(string.Join(
            "\n",
            contentHashes.Select(pair => pair.Key.FilePath + "|" + pair.Value).Order(StringComparer.Ordinal)));

        return new Run(this, contentHashes, runDigest, AnalysisConfigurationHash(configuration));
    }

    // Only the settings that change the unscored metrics belong here; scores, thresholds and display flags are applied after the cache.
    private static string AnalysisConfigurationHash(CognitiveConfiguration configuration) =>
        Sha256(JsonSerializer.Serialize(new
        {
            configuration.CountElseAsNesting,
            configuration.CountElseIfAsNesting,
            ExcludePatterns = configuration.ExcludePatterns,
        }));

    private static string Sha256(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private string EntryPath(string key) => Path.Combine(_directory, key[..2], key + ".json");

    private List<CognitiveMetrics>? Load(string key, string filePath)
    {
        try
        {
            Payload? payload = JsonSerializer.Deserialize<Payload>(File.ReadAllText(EntryPath(key), Encoding.UTF8), JsonOptions);
            return payload is { Version: FormatVersion, Methods: not null } && payload.Methods.All(IsComplete)
                ? payload.Methods.Select(method => method.ToMetrics(filePath)).ToList()
                : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            // An unreadable entry is a miss; the file is analysed again and the entry is overwritten.
            return null;
        }
    }

    private static bool IsComplete(CachedMethod? method) =>
        method is { MethodName: not null, ClassName: not null, MethodSignature: not null, Understandability: not null };

    private void Store(string key, IEnumerable<CognitiveMetrics> metrics)
    {
        string? temporary = null;
        try
        {
            string path = EntryPath(key);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            temporary = $"{path}.{Guid.NewGuid():N}.tmp";
            string json = JsonSerializer.Serialize(new Payload(FormatVersion, metrics.Select(CachedMethod.From).ToList()), JsonOptions);
            File.WriteAllText(temporary, json, new UTF8Encoding(false));
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or JsonException)
        {
            TryDelete(temporary);
            if (Interlocked.Exchange(ref _writeFailureReported, 1) == 0)
            {
                _warn?.Invoke($"The analysis cache in '{_directory}' cannot be written ({e.Message}). Continuing without it.");
            }
        }
    }

    private static void TryDelete(string? path)
    {
        try
        {
            if (path is not null)
            {
                File.Delete(path);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The temporary file is a leftover at worst; the cache directory is safe to delete.
        }
    }

    /// <summary>The cache for one set of sources.</summary>
    public sealed class Run
    {
        private readonly MetricsCache _cache;
        private readonly IReadOnlyDictionary<SyntaxTree, string> _contentHashes;
        private readonly string _runDigest;
        private readonly string _configurationHash;

        internal Run(MetricsCache cache, IReadOnlyDictionary<SyntaxTree, string> contentHashes, string runDigest, string configurationHash)
        {
            _cache = cache;
            _contentHashes = contentHashes;
            _runDigest = runDigest;
            _configurationHash = configurationHash;
        }

        public List<CognitiveMetrics>? TryLoad(SyntaxTree tree) => _cache.Load(KeyOf(tree), tree.FilePath);

        public void Store(SyntaxTree tree, IEnumerable<CognitiveMetrics> metrics) => _cache.Store(KeyOf(tree), metrics);

        internal string KeyOf(SyntaxTree tree) =>
            Sha256(string.Join("|", tree.FilePath, _contentHashes[tree], _runDigest, _configurationHash, _cache._toolFingerprint));
    }

    internal sealed record Payload(int Version, List<CachedMethod>? Methods);

    internal sealed record CachedMethod(
        string MethodName,
        string ClassName,
        string MethodSignature,
        int Line,
        int IfCount,
        int ElseCount,
        int LoopCount,
        int SwitchCount,
        int TryCatchCount,
        int ReturnCount,
        int ArgumentCount,
        int LinesOfCode,
        int NestingLevels,
        double CyclomaticComplexity,
        int LocalVariableCount,
        int FieldAccessCount,
        int PropertyAccessCount,
        HalsteadMetrics? Halstead,
        Understandability Understandability
    ) {
        public static CachedMethod From(CognitiveMetrics m) => new(
            m.MethodName, m.ClassName, m.methodSignature, m.methodLineNumber,
            m.ifCount, m.elseCount, m.loopCount, m.switchCount, m.tryCatchCount, m.returnCount,
            m.argumentCount, m.linesOfCode, m.nestingLevels, m.cyclomaticComplexity,
            m.localVariableCount, m.fieldAccessCount, m.propertyAccessCount,
            m.Halstead, m.Understandability);

        public CognitiveMetrics ToMetrics(string filePath) => new(
            methodName: MethodName,
            className: ClassName,
            filePath: filePath,
            methodSignature: MethodSignature,
            methodLineNumber: Line,
            ifCount: IfCount,
            elseCount: ElseCount,
            loopCount: LoopCount,
            switchCount: SwitchCount,
            tryCatchCount: TryCatchCount,
            returnCount: ReturnCount,
            argumentCount: ArgumentCount,
            linesOfCode: LinesOfCode,
            nestingLevels: NestingLevels,
            cyclomaticComplexity: CyclomaticComplexity,
            localVariableCount: LocalVariableCount,
            fieldAccessCount: FieldAccessCount,
            propertyAccessCount: PropertyAccessCount,
            halstead: Halstead,
            understandability: Understandability);
    }
}
