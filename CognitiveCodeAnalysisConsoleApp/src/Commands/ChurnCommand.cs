/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Application;
using CognitiveCodeAnalysis.Churn;
using CognitiveCodeAnalysis.CodeCoverage;
using CognitiveCodeAnalysis.CognitiveAnalysis;
using CognitiveCodeAnalysis.Configuration;
using CognitiveCodeAnalysisConsoleApp.Infrastructure;

using Spectre.Console;
using Spectre.Console.Cli;

namespace CognitiveCodeAnalysisConsoleApp.Commands;

/// <summary>
/// Churn = cognitive score of a class × number of git changes of its file. Not the same as the "Churn" column of the
/// analyse reports, which is the score weighted with the missing coverage (risk churn).
/// </summary>
internal sealed class ChurnCommand(
    CognitiveAnalysisFacade facade,
    ICoverageReader coverageReader,
    IConsoleNotifier consoleNotifier,
    TimeProvider timeProvider
) : Command<ChurnCommandSettings> {

    private const int Success = 0;
    private const int Error = -1;
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(60);

    public override ValidationResult Validate(CommandContext context, ChurnCommandSettings settings)
    {
        DateOnly today = DateOnly.FromDateTime(timeProvider.GetLocalNow().DateTime);
        return settings.SinceDate > today
            ? ValidationResult.Error("--since must not be in the future.")
            : ValidationResult.Success();
    }

    public override int Execute(
        CommandContext context,
        ChurnCommandSettings settings,
        CancellationToken cancellationToken
    ) {
        try
        {
            return Run(settings);
        }
        catch (Exception exception)
        {
            consoleNotifier.WriteError(exception.Message);
            return Error;
        }
    }

    private int Run(ChurnCommandSettings settings)
    {
        string[] sourcePaths = SourceFileFinder.SplitPathList(settings.SourcePath ?? Directory.GetCurrentDirectory());
        CognitiveConfiguration configuration = CognitiveConfigurationFactory.Load(settings.ConfigFile);
        foreach (string warning in configuration.Warnings)
        {
            consoleNotifier.WriteWarning(warning);
        }

        List<string> files = facade.FindSourceFiles(sourcePaths, configuration.ExcludeFilePatterns, progress: null);
        if (files.Count == 0)
        {
            consoleNotifier.WriteNoSourceFilesFound(string.Join(", ", sourcePaths));
            return Error;
        }

        ClassCoverageIndex? coverage = LoadCoverage(settings);
        CognitiveMetricsCollection metrics = facade.AnalyseSourceFiles(files, configuration);
        var counter = new GitChangeCounter(GitChangeCounter.DefaultExecutable, settings.SinceDate, GitTimeout);
        List<ChurnMetrics> churn = GitChurnCalculator.Calculate(metrics, counter, coverage);

        if (coverage is not null)
        {
            WarnAboutClassesWithoutCoverage(churn);
        }

        if (settings.ReportTypeValue is { } type)
        {
            string outputFile = Path.GetFullPath(settings.OutputFile!);
            new ChurnReportBuilder(timeProvider, coverage is not null).Write(type, outputFile, churn);
            consoleNotifier.WriteReportGenerated("Churn", outputFile);
            return Success;
        }

        AnsiConsole.Write(ChurnTable.Build(churn, coverage is not null));
        return Success;
    }

    private ClassCoverageIndex? LoadCoverage(ChurnCommandSettings settings)
    {
        CoverageFormat format = string.IsNullOrWhiteSpace(settings.CoverageClover) ? CoverageFormat.Auto : CoverageFormat.Clover;
        string? coverageFile = format == CoverageFormat.Clover ? settings.CoverageClover : settings.CoverageCobertura;
        if (string.IsNullOrWhiteSpace(coverageFile))
        {
            return null;
        }

        string fullPath = Path.GetFullPath(coverageFile);
        if (!File.Exists(fullPath))
        {
            throw new ChurnException($"Coverage file not found: {fullPath}");
        }

        try
        {
            return new ClassCoverageIndex(format.ReaderFor(coverageReader).ReadCoverage(fullPath).ToList());
        }
        catch (InvalidCoverageFileException exception)
        {
            throw new ChurnException(exception.Message, exception);
        }
        catch (Exception exception)
        {
            throw new ChurnException($"Failed to load coverage data: {exception.Message}", exception);
        }
    }

    private void WarnAboutClassesWithoutCoverage(List<ChurnMetrics> churn)
    {
        int missing = churn.Count(metric => metric.Coverage is null);
        if (missing > 0)
        {
            consoleNotifier.WriteWarning(
                $"{missing} of {churn.Count} classes are not in the coverage report; their coverage, risk churn and risk level are N/A.");
        }
    }
}
