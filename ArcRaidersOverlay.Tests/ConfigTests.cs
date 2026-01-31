using ArcRaidersOverlay;
using Xunit;

namespace ArcRaidersOverlay.Tests;

public class RegionConfigTests
{
    [Fact]
    public void IsValid_PositiveDimensions_ReturnsTrue()
    {
        var region = new RegionConfig { X = 0, Y = 0, Width = 100, Height = 100 };

        Assert.True(region.IsValid);
    }

    [Fact]
    public void IsValid_ZeroWidth_ReturnsFalse()
    {
        var region = new RegionConfig { X = 0, Y = 0, Width = 0, Height = 100 };

        Assert.False(region.IsValid);
    }

    [Fact]
    public void IsValid_ZeroHeight_ReturnsFalse()
    {
        var region = new RegionConfig { X = 0, Y = 0, Width = 100, Height = 0 };

        Assert.False(region.IsValid);
    }

    [Fact]
    public void IsValid_NegativeWidth_ReturnsFalse()
    {
        var region = new RegionConfig { X = 0, Y = 0, Width = -10, Height = 100 };

        Assert.False(region.IsValid);
    }

    [Fact]
    public void IsValid_NegativeHeight_ReturnsFalse()
    {
        var region = new RegionConfig { X = 0, Y = 0, Width = 100, Height = -10 };

        Assert.False(region.IsValid);
    }

    [Fact]
    public void IsValid_NegativeCoordinates_StillValid()
    {
        // Negative X/Y are valid (multi-monitor setups)
        var region = new RegionConfig { X = -100, Y = -50, Width = 100, Height = 100 };

        Assert.True(region.IsValid);
    }
}

public class AppConfigTests
{
    [Fact]
    public void DefaultConfig_HasValidDefaults()
    {
        var config = new AppConfig();

        Assert.Equal(15, config.EventPollIntervalSeconds);
        Assert.True(config.UseGameRelativeCoordinates);
        Assert.True(config.FollowGameWindow);
        Assert.Equal(10, config.OverlayOffsetX);
        Assert.Equal(10, config.OverlayOffsetY);
    }

    [Fact]
    public void DefaultConfig_RegionsAreInitialized()
    {
        var config = new AppConfig();

        Assert.NotNull(config.EventsRegion);
        Assert.NotNull(config.TooltipRegion);
    }
}
