using ArcRaidersOverlay;
using Xunit;

namespace ArcRaidersOverlay.Tests;

public class ThemeTests
{
    [Fact]
    public void TextColors_AreNotTransparent()
    {
        // Text colors should be visible (not fully transparent)
        Assert.NotEqual(0, Theme.TextMuted.A + Theme.TextMuted.R + Theme.TextMuted.G + Theme.TextMuted.B);
        Assert.NotEqual(0, Theme.TextSecondary.A + Theme.TextSecondary.R + Theme.TextSecondary.G + Theme.TextSecondary.B);
        Assert.NotEqual(0, Theme.TextDefault.A + Theme.TextDefault.R + Theme.TextDefault.G + Theme.TextDefault.B);
    }

    [Fact]
    public void EventColors_AreDifferent()
    {
        // Each event type should have a distinct color
        Assert.NotEqual(Theme.EventDrop, Theme.EventStorm);
        Assert.NotEqual(Theme.EventDrop, Theme.EventConvoy);
        Assert.NotEqual(Theme.EventDrop, Theme.EventExtraction);
        Assert.NotEqual(Theme.EventStorm, Theme.EventConvoy);
    }

    [Fact]
    public void TimerColors_FollowSeverityGradient()
    {
        // Timer colors should be distinguishable
        Assert.NotEqual(Theme.TimerActive, Theme.TimerUrgent);
        Assert.NotEqual(Theme.TimerUrgent, Theme.TimerWarning);
        Assert.NotEqual(Theme.TimerWarning, Theme.TimerNormal);
    }

    [Fact]
    public void Brushes_AreNotNull()
    {
        Assert.NotNull(Theme.BrushTextMuted);
        Assert.NotNull(Theme.BrushTextSecondary);
        Assert.NotNull(Theme.BrushTextDefault);
        Assert.NotNull(Theme.BrushEventDrop);
        Assert.NotNull(Theme.BrushEventStorm);
        Assert.NotNull(Theme.BrushTimerActive);
        Assert.NotNull(Theme.BrushTransparent);
    }

    [Fact]
    public void Brushes_AreFrozen()
    {
        // Frozen brushes are thread-safe and more performant
        Assert.True(Theme.BrushTextMuted.IsFrozen);
        Assert.True(Theme.BrushTextSecondary.IsFrozen);
        Assert.True(Theme.BrushTextDefault.IsFrozen);
        Assert.True(Theme.BrushEventDrop.IsFrozen);
        Assert.True(Theme.BrushTimerActive.IsFrozen);
        Assert.True(Theme.BrushTransparent.IsFrozen);
    }

    [Fact]
    public void RecommendationColors_HaveAlphaForTransparency()
    {
        // Recommendation banner colors should have some transparency
        Assert.True(Theme.RecommendKeep.A < 255);
        Assert.True(Theme.RecommendSell.A < 255);
        Assert.True(Theme.RecommendRecycle.A < 255);
        Assert.True(Theme.RecommendEither.A < 255);
    }

    [Fact]
    public void RecycleEfficiencyColors_AreDistinct()
    {
        // Good/Medium/Poor should be easily distinguishable
        Assert.NotEqual(Theme.RecycleGood, Theme.RecycleMedium);
        Assert.NotEqual(Theme.RecycleMedium, Theme.RecyclePoor);
        Assert.NotEqual(Theme.RecycleGood, Theme.RecyclePoor);
    }
}
