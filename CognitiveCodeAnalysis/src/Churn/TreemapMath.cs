/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

namespace CognitiveCodeAnalysis.Churn;

/// <summary>
/// The maths behind the SVG treemap, ported from the reference implementation: item selection, score normalisation,
/// colours and the slice-and-dice layout.
/// </summary>
internal static class TreemapMath
{
    private const double ColorMin = 0;
    private const double ColorMax = 10;

    internal sealed record Item(string ClassName, double Churn, double Score);

    internal sealed record Rect(double X, double Y, double Width, double Height, Item Item);

    /// <summary>The classes with churn above zero, biggest churn first.</summary>
    internal static List<Item> PrepareItems(IEnumerable<ChurnMetrics> metrics) =>
        metrics
            .Where(metric => metric.Churn > 0)
            .Select(metric => new Item(metric.ClassName, metric.Churn, metric.Score))
            .OrderByDescending(item => item.Churn) // stable, like PHP 8's usort
            .ToList();

    /// <summary>The score range mapped to the colour scale; the fixed 0 to 10 scale when there is no spread.</summary>
    internal static (double Min, double Max) FindScoreRange(IReadOnlyList<Item> items)
    {
        if (items.Count == 0)
        {
            return (ColorMin, ColorMax);
        }

        double min = items.Min(item => item.Score);
        double max = items.Max(item => item.Score);
        return min == max ? (ColorMin, ColorMax) : (min, max);
    }

    /// <summary>Maps the score onto 0 to 10; 0 when the range is empty.</summary>
    internal static double NormalizeScore(double score, double min, double max) =>
        max > min ? ColorMax * (score - min) / (max - min) : 0;

    /// <summary>Green for 0 to red for 10.</summary>
    internal static string ScoreToColor(double score)
    {
        double clamped = Math.Max(ColorMin, Math.Min(ColorMax, score));
        int red = (int)(255 * (clamped / ColorMax));
        int green = (int)(180 * (1 - (clamped / ColorMax)));
        return $"rgb({red},{green},80)";
    }

    /// <summary>Slice-and-dice layout, starting with a vertical cut and alternating the orientation per level.</summary>
    internal static List<Rect> Layout(IReadOnlyList<Item> items, double width, double height)
    {
        var rects = new List<Rect>();
        Layout(items, 0, 0, width, height, vertical: true, rects);
        return rects;
    }

    private static void Layout(
        IReadOnlyList<Item> items, double x, double y, double width, double height, bool vertical, List<Rect> rects)
    {
        if (items.Count == 0)
        {
            return;
        }

        if (items.Count == 1)
        {
            rects.Add(new Rect(x, y, width, height, items[0]));
            return;
        }

        double sum = items.Sum(item => item.Churn);
        int split = FindSplitIndex(items, sum);
        List<Item> first = items.Take(split).ToList();
        List<Item> second = items.Skip(split).ToList();
        double share = first.Sum(item => item.Churn) / sum;

        if (vertical)
        {
            double firstWidth = width * share;
            Layout(first, x, y, firstWidth, height, false, rects);
            Layout(second, x + firstWidth, y, width - firstWidth, height, false, rects);
            return;
        }

        double firstHeight = height * share;
        Layout(first, x, y, width, firstHeight, true, rects);
        Layout(second, x, y + firstHeight, width, height - firstHeight, true, rects);
    }

    /// <summary>The count of items up to and including the first one that brings the running sum to half of the total.</summary>
    private static int FindSplitIndex(IReadOnlyList<Item> items, double sum)
    {
        double accumulated = 0;
        for (int i = 0; i < items.Count; i++)
        {
            accumulated += items[i].Churn;
            if (accumulated >= sum / 2)
            {
                int split = i + 1;
                return split >= items.Count ? 1 : split;
            }
        }

        return 1;
    }
}
