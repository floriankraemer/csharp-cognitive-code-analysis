/// <copyright company="Florian Krämer">
///     Licensed under the MIT license. See LICENSE file in the project root for full license information.
/// </copyright>

using CognitiveCodeAnalysis.Churn;

namespace CognitiveCodeAnalysis.Tests.Churn;

public class TreemapMathTests
{
    private static TreemapMath.Item Item(string name, double churn, double score = 1) => new(name, churn, score);

    [Test]
    public void PrepareItems_DropsClassesWithoutChurnAndSortsBiggestFirstKeepingTiesStable()
    {
        List<TreemapMath.Item> items = TreemapMath.PrepareItems(
        [
            new("a", "f", 1, 1, 1), new("zero", "f", 1, 0, 0), new("b", "f", 1, 1, 1), new("big", "f", 1, 5, 5),
            new("negative", "f", 1, 1, -1),
        ]);

        Assert.That(items.Select(item => item.ClassName), Is.EqualTo(new[] { "big", "a", "b" }));
    }

    [Test]
    public void FindScoreRange_UsesTheFixedScaleWithoutSpread()
    {
        Assert.That(TreemapMath.FindScoreRange([]), Is.EqualTo((0.0, 10.0)));
        Assert.That(TreemapMath.FindScoreRange([Item("a", 1, 3), Item("b", 1, 3)]), Is.EqualTo((0.0, 10.0)));
        Assert.That(TreemapMath.FindScoreRange([Item("a", 1, 3), Item("b", 1, 7), Item("c", 1, 5)]), Is.EqualTo((3.0, 7.0)));
    }

    [Test]
    public void NormalizeScore_MapsTheRangeOntoZeroToTen()
    {
        Assert.That(TreemapMath.NormalizeScore(5, 0, 10), Is.EqualTo(5));
        Assert.That(TreemapMath.NormalizeScore(3, 3, 7), Is.Zero);
        Assert.That(TreemapMath.NormalizeScore(7, 3, 7), Is.EqualTo(10));
        Assert.That(TreemapMath.NormalizeScore(5, 5, 5), Is.Zero);
    }

    [TestCase(0, "rgb(0,180,80)")]
    [TestCase(10, "rgb(255,0,80)")]
    [TestCase(5, "rgb(127,90,80)")]
    [TestCase(-3, "rgb(0,180,80)")]
    [TestCase(42, "rgb(255,0,80)")]
    [TestCase(0.9, "rgb(22,163,80)")]
    public void ScoreToColor_GoesFromGreenToRedAndClampsTheScore(double score, string expected)
    {
        Assert.That(TreemapMath.ScoreToColor(score), Is.EqualTo(expected));
    }

    [Test]
    public void Layout_OfNoItemsIsEmpty()
    {
        Assert.That(TreemapMath.Layout([], 1200, 800), Is.Empty);
    }

    [Test]
    public void Layout_OfOneItemFillsTheCanvas()
    {
        TreemapMath.Rect rect = TreemapMath.Layout([Item("a", 3)], 1200, 800).Single();

        Assert.That((rect.X, rect.Y, rect.Width, rect.Height), Is.EqualTo((0.0, 0.0, 1200.0, 800.0)));
    }

    [Test]
    public void Layout_CutsVerticallyFirstThenHorizontallyAtTheHalfwayItem()
    {
        // 6 | 3 + 1: the first item reaches half of the sum, so it gets 60% of the width.
        List<TreemapMath.Rect> rects = TreemapMath.Layout([Item("a", 6), Item("b", 3), Item("c", 1)], 1000, 500);

        Assert.That(rects.Select(rect => rect.Item.ClassName), Is.EqualTo(new[] { "a", "b", "c" }));
        Assert.That(rects[0], Has.Property("X").EqualTo(0).And.Property("Width").EqualTo(600).And.Property("Height").EqualTo(500));
        Assert.That(rects[1], Has.Property("X").EqualTo(600).And.Property("Y").EqualTo(0).And.Property("Width").EqualTo(400));
        Assert.That(rects[1].Height, Is.EqualTo(500 * 0.75).Within(1e-9));
        Assert.That(rects[2].Y, Is.EqualTo(500 * 0.75).Within(1e-9));
    }

    [Test]
    public void Layout_NeverLeavesTheCanvasAndFillsItsWholeArea()
    {
        var items = Enumerable.Range(1, 25).Select(i => Item("c" + i, 26 - i)).ToList();

        List<TreemapMath.Rect> rects = TreemapMath.Layout(items, 1200, 800);

        Assert.That(rects, Has.Count.EqualTo(25));
        Assert.That(rects.Sum(rect => rect.Width * rect.Height), Is.EqualTo(1200 * 800).Within(1e-6));
        Assert.That(rects, Has.All.Matches<TreemapMath.Rect>(rect =>
            rect.X >= 0 && rect.Y >= 0 && rect.X + rect.Width <= 1200 + 1e-9 && rect.Y + rect.Height <= 800 + 1e-9));
    }

    [Test]
    public void Layout_GivesEachItemAnAreaProportionalToItsChurn()
    {
        List<TreemapMath.Rect> rects = TreemapMath.Layout([Item("a", 7), Item("b", 2), Item("c", 1)], 1000, 1000);

        Assert.That(rects[0].Width * rects[0].Height, Is.EqualTo(700000).Within(1e-6));
        Assert.That(rects[1].Width * rects[1].Height, Is.EqualTo(200000).Within(1e-6));
        Assert.That(rects[2].Width * rects[2].Height, Is.EqualTo(100000).Within(1e-6));
    }

    [Test]
    public void Layout_SplitsAfterTheFirstItemWhenItAloneReachesHalf()
    {
        // The first item alone reaches half of the sum, so the cut goes right after it.
        List<TreemapMath.Rect> rects = TreemapMath.Layout([Item("a", 1), Item("b", 1)], 100, 100);

        Assert.That(rects.Select(rect => rect.Width), Is.EqualTo(new[] { 50.0, 50.0 }));
    }
}
