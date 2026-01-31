using System;
using ArcRaidersOverlay;
using Xunit;

namespace ArcRaidersOverlay.Tests;

public class EventParserTests
{
    [Fact]
    public void Parse_PrimaryPattern_ReturnsEvent()
    {
        var text = "Supply Drop - Dread Canyon - 5:32";

        var events = EventParser.Parse(text);

        Assert.Single(events);
        Assert.Equal("Supply Drop", events[0].Name);
        Assert.Equal("Dread Canyon", events[0].Location);
        Assert.Equal("5:32", events[0].Timer);
    }

    [Fact]
    public void Parse_AlternatePattern_ReturnsEvent()
    {
        var text = "Convoy in Blackstone Quarry (1:05)";

        var events = EventParser.Parse(text);

        Assert.Single(events);
        Assert.Equal("Convoy", events[0].Name);
        Assert.Equal("Blackstone Quarry", events[0].Location);
        Assert.Equal("1:05", events[0].Timer);
    }

    [Fact]
    public void Parse_NoStructuredMatch_ReturnsUnknownEvents()
    {
        var text = "Random text 3:21 with timer only";

        var events = EventParser.Parse(text);

        Assert.Single(events);
        Assert.Equal("Unknown Event", events[0].Name);
        Assert.Equal("Unknown", events[0].Location);
        Assert.Equal("3:21", events[0].Timer);
    }

    [Fact]
    public void DetectMap_ReturnsConfiguredMapName()
    {
        var text = "Extraction at Dread Canyon in 4:00";

        var map = EventParser.DetectMap(text);

        Assert.Equal("Dread Canyon", map);
    }

    [Theory]
    [InlineData("ACTIVE", 0, 0)]
    [InlineData("1:30", 1, 30)]
    public void ParseTimer_HandlesActiveAndClock(string input, int minutes, int seconds)
    {
        var result = EventParser.ParseTimer(input);

        Assert.NotNull(result);
        Assert.Equal(new TimeSpan(0, minutes, seconds), result);
    }
}
