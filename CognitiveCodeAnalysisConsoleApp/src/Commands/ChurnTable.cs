/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using System.Globalization;

using CognitiveCodeAnalysis.Churn;

using Spectre.Console;
using Spectre.Console.Rendering;

namespace CognitiveCodeAnalysisConsoleApp.Commands;

/// <summary>The console table of the churn command: the classes that changed and have a score.</summary>
internal static class ChurnTable
{
    public static Table Build(IEnumerable<ChurnMetrics> churn, bool withCoverage)
    {
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("Class"));
        table.AddColumn(new TableColumn("Score").RightAligned());
        table.AddColumn(new TableColumn("Churn").RightAligned());
        if (withCoverage)
        {
            table.AddColumn(new TableColumn("Risk Churn").RightAligned());
        }

        table.AddColumn(new TableColumn("Times Changed").RightAligned());
        if (withCoverage)
        {
            table.AddColumn(new TableColumn("Coverage").RightAligned());
            table.AddColumn(new TableColumn("Risk Level"));
        }

        foreach (ChurnMetrics metric in churn.Where(metric => metric.Score != 0 && metric.Churn != 0))
        {
            var cells = new List<string>
            {
                metric.ClassName,
                metric.Score.ToString("0.###", CultureInfo.InvariantCulture),
                metric.Churn.ToString("F3", CultureInfo.InvariantCulture),
            };
            if (withCoverage)
            {
                cells.Add(metric.RiskChurn is { } risk ? risk.ToString("F3", CultureInfo.InvariantCulture) : "N/A");
            }

            cells.Add(metric.TimesChanged.ToString(CultureInfo.InvariantCulture));
            if (withCoverage)
            {
                cells.Add(metric.Coverage is { } coverage
                    ? (coverage * 100).ToString("F2", CultureInfo.InvariantCulture) + "%"
                    : "N/A");
                cells.Add(metric.RiskLevel is { } level ? level.ToString().ToUpperInvariant() : "N/A");
            }

            table.AddRow(cells.Select(cell => (IRenderable)new Text(cell)).ToArray());
        }

        return table;
    }
}
