/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.Configuration;

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.Commands;

/// <summary>
/// Asks the questions of the <c>init</c> command. Each question is preceded by a dimmed explanation. An empty answer
/// takes the default shown in brackets, and an invalid answer is reported and asked again.
/// </summary>
internal static class ConfigInitPrompter
{
    private const string ThresholdExplanation =
        "Methods with a score above this threshold are considered complex and are highlighted.";

    public static ConfigInitAnswers Ask(ConfigInitAnswers defaults)
    {
        double scoreThreshold = AskScoreThreshold(defaults.ScoreThreshold);
        bool showOnlyMethodsExceedingThreshold = AskYesNo(
            "Show only methods whose score exceeds the threshold, to focus on the most complex ones.",
            "Show only methods exceeding the threshold?",
            defaults.ShowOnlyMethodsExceedingThreshold);
        bool showHalsteadComplexity = AskYesNo(
            "Include Halstead complexity (program length and vocabulary) in the output.",
            "Show Halstead complexity?",
            defaults.ShowHalsteadComplexity);
        bool showCyclomaticComplexity = AskYesNo(
            "Include cyclomatic complexity (the number of independent paths) in the output.",
            "Show cyclomatic complexity?",
            defaults.ShowCyclomaticComplexity);
        bool showCouplingMetrics = AskYesNo(
            "Include class coupling metrics in the output.",
            "Show coupling metrics?",
            defaults.ShowCouplingMetrics);
        bool showDetailedCognitiveMetrics = AskYesNo(
            "Show the individual metric columns, not only the overall score.",
            "Show detailed cognitive metrics?",
            defaults.ShowDetailedCognitiveMetrics);
        bool groupByClass = AskYesNo(
            "Group the results into one table per class instead of a single flat table.",
            "Group results by class?",
            defaults.GroupByClass);

        return new ConfigInitAnswers(
            scoreThreshold,
            showOnlyMethodsExceedingThreshold,
            showHalsteadComplexity,
            showCyclomaticComplexity,
            showCouplingMetrics,
            showDetailedCognitiveMetrics,
            groupByClass
        );
    }

    private static double AskScoreThreshold(double defaultValue)
    {
        Explain(ThresholdExplanation);
        var prompt = new TextPrompt<string>("Score threshold?")
            .DefaultValue(defaultValue.ToString(CultureInfo.InvariantCulture))
            .Validate(input => IsValidScoreThreshold(input)
                ? ValidationResult.Success()
                : ValidationResult.Error("Score threshold must be a number greater than 0, for example 0.5."));

        return double.Parse(AnsiConsole.Prompt(prompt), CultureInfo.InvariantCulture);
    }

    private static bool AskYesNo(string explanation, string question, bool defaultValue)
    {
        Explain(explanation);
        return AnsiConsole.Prompt(new ConfirmationPrompt(question) { DefaultValue = defaultValue });
    }

    private static void Explain(string explanation) =>
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(explanation)}[/]");

    private static bool IsValidScoreThreshold(string input) =>
        double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
        && double.IsFinite(value)
        && value > 0;
}
