/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.Configuration;

using Spectre.Console;

namespace CognitiveCodeAnalysisConsoleApp.Commands;

/// <summary>
/// Asks the questions of the <c>init</c> command. An empty answer takes the default shown in brackets, and an invalid
/// answer is reported and asked again.
/// </summary>
internal static class ConfigInitPrompter
{
    public static ConfigInitAnswers Ask(ConfigInitAnswers defaults)
    {
        double scoreThreshold = AskScoreThreshold(defaults.ScoreThreshold);
        bool showOnlyMethodsExceedingThreshold = AskYesNo(
            "Show only methods exceeding the threshold?",
            defaults.ShowOnlyMethodsExceedingThreshold);
        bool showHalsteadComplexity = AskYesNo("Show Halstead complexity?", defaults.ShowHalsteadComplexity);
        bool showCyclomaticComplexity = AskYesNo("Show cyclomatic complexity?", defaults.ShowCyclomaticComplexity);
        bool showCouplingMetrics = AskYesNo("Show coupling metrics?", defaults.ShowCouplingMetrics);
        bool showDetailedCognitiveMetrics = AskYesNo(
            "Show detailed cognitive metrics?",
            defaults.ShowDetailedCognitiveMetrics);
        bool groupByClass = AskYesNo("Group results by class?", defaults.GroupByClass);

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
        var prompt = new TextPrompt<string>("Score threshold?")
            .DefaultValue(defaultValue.ToString(CultureInfo.InvariantCulture))
            .Validate(input => IsValidScoreThreshold(input)
                ? ValidationResult.Success()
                : ValidationResult.Error("Score threshold must be a number greater than 0, for example 0.5."));

        return double.Parse(AnsiConsole.Prompt(prompt), CultureInfo.InvariantCulture);
    }

    private static bool AskYesNo(string question, bool defaultValue) =>
        AnsiConsole.Prompt(new ConfirmationPrompt(question) { DefaultValue = defaultValue });

    private static bool IsValidScoreThreshold(string input) =>
        double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
        && double.IsFinite(value)
        && value > 0;
}
