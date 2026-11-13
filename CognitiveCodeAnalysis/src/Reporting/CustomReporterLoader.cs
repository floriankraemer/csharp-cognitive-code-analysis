/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Reflection;

using CognitiveCodeAnalysis.Churn;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.CognitiveAnalysis.Baseline;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysis.Reporting.Spi;

namespace CognitiveCodeAnalysis.Reporting;

/// <summary>Finds, loads and runs the reporters of plugin assemblies named in <c>customReporters</c>.</summary>
public static class CustomReporterLoader
{
    /// <summary>Renders the analyse report of the custom reporter <paramref name="key"/> into <paramref name="outputFile"/>.</summary>
    public static void RunCognitive(
        string key,
        CognitiveConfiguration configuration,
        string outputFile,
        CognitiveMetricsCollection metrics,
        CognitiveBaselineComparison? baseline) =>
        Run<ICustomCognitiveReporter>(
            key,
            configuration.CustomReporters.Cognitive[key],
            configuration.ConfigDirectory,
            outputFile,
            reporter => reporter.Render(CustomReportInputFactory.Cognitive(metrics, configuration, baseline)));

    /// <summary>Renders the churn report of the custom reporter <paramref name="key"/> into <paramref name="outputFile"/>.</summary>
    public static void RunChurn(
        string key,
        CognitiveConfiguration configuration,
        string outputFile,
        IReadOnlyList<ChurnMetrics> churn,
        bool coverageProvided,
        DateOnly since) =>
        Run<ICustomChurnReporter>(
            key,
            configuration.CustomReporters.Churn[key],
            configuration.ConfigDirectory,
            outputFile,
            reporter => reporter.Render(CustomReportInputFactory.Churn(churn, coverageProvided, since)));

    /// <summary>
    /// The error for a custom key that has the name of a built-in report. Built-in names win, so the key can never be
    /// used; this is raised only when that name is requested with <c>-r</c>.
    /// </summary>
    public static CustomReporterException Collision(string builtInName, string section) =>
        new($"The custom reporter key in customReporters.{section} collides with the built-in report type '{builtInName}'. "
            + "Rename the key in the configuration.");

    /// <summary>
    /// Loads the reporter, renders it and writes the result to <paramref name="outputFile"/> in one step, so readers never see a half-written file.
    /// </summary>
    /// <typeparam name="T">The reporter interface: <see cref="Spi.ICustomCognitiveReporter"/> or <see cref="Spi.ICustomChurnReporter"/>.</typeparam>
    /// <exception cref="CustomReporterException">The reporter cannot be loaded, fails, or returns nothing.</exception>
    internal static void Run<T>(
        string key,
        CustomReporterEntry entry,
        string? configDirectory,
        string outputFile,
        Func<T, string?> render)
        where T : class
    {
        T reporter = Create<T>(key, entry, configDirectory);
        string? result;
        try
        {
            result = render(reporter);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Exception cause = Unwrap(exception);
            throw new CustomReporterException(
                $"Custom reporter '{key}' failed while rendering: {cause.GetType().Name}: {cause.Message}", cause);
        }

        if (result is null)
        {
            throw new CustomReporterException($"Custom reporter '{key}' returned null instead of the report text.");
        }

        ChurnReportFileWriter.Write(outputFile, result);
    }

    private static T Create<T>(string key, CustomReporterEntry entry, string? configDirectory) where T : class
    {
        if (string.IsNullOrWhiteSpace(entry.Type) || string.IsNullOrWhiteSpace(entry.Assembly))
        {
            throw new CustomReporterException(
                $"Custom reporter '{key}' needs both \"type\" and \"assembly\" in the customReporters section of the configuration.");
        }

        string path = Path.GetFullPath(entry.Assembly, configDirectory ?? Directory.GetCurrentDirectory());
        if (!File.Exists(path))
        {
            throw new CustomReporterException($"Custom reporter '{key}': assembly not found: {path}");
        }

        Type type = FindType(key, entry.Type, path);
        if (!typeof(T).IsAssignableFrom(type))
        {
            throw new CustomReporterException(
                $"Custom reporter '{key}': type {type.FullName} does not implement {typeof(T).FullName}.");
        }

        if (type.IsAbstract || type.GetConstructor(Type.EmptyTypes) is not { IsPublic: true } constructor)
        {
            throw new CustomReporterException(
                $"Custom reporter '{key}': type {type.FullName} must be a class with a public parameterless constructor.");
        }

        try
        {
            return (T)constructor.Invoke(null);
        }
        catch (TargetInvocationException exception)
        {
            Exception cause = Unwrap(exception);
            throw new CustomReporterException(
                $"Custom reporter '{key}': the constructor of {type.FullName} threw {cause.GetType().Name}: {cause.Message}", cause);
        }
    }

    private static Type FindType(string key, string typeName, string path)
    {
        try
        {
            Type? type = new PluginLoadContext(path).LoadFromAssemblyPath(path).GetType(typeName, throwOnError: false);
            return type ?? throw new CustomReporterException(
                $"Custom reporter '{key}': type {typeName} not found in {path}.");
        }
        catch (Exception exception) when (exception is BadImageFormatException or FileLoadException
            or FileNotFoundException or TypeLoadException)
        {
            throw new CustomReporterException(
                $"Custom reporter '{key}': {path} or one of its dependencies cannot be loaded: {exception.Message}", exception);
        }
    }

    /// <summary>The exception a constructor or a type initializer really threw.</summary>
    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException or TypeInitializationException && exception.InnerException is { } inner)
        {
            exception = inner;
        }

        return exception;
    }
}
