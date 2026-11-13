/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Churn;

public enum ChurnReportType
{
    Json,
    Csv,
    Html,
    Markdown,
    SvgTreemap,
}

public static class ChurnReportTypes
{
    public const string Accepted = "Json, Csv, Html, Markdown, SvgTreemap";

    /// <summary>Parses a report type in any letter case; <c>svg</c> and <c>svg-treemap</c> are aliases of SvgTreemap.</summary>
    public static bool TryParse(string? value, out ChurnReportType type)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "json": type = ChurnReportType.Json; return true;
            case "csv": type = ChurnReportType.Csv; return true;
            case "html": type = ChurnReportType.Html; return true;
            case "markdown": type = ChurnReportType.Markdown; return true;
            case "svgtreemap" or "svg-treemap" or "svg": type = ChurnReportType.SvgTreemap; return true;
            default: type = default; return false;
        }
    }
}
