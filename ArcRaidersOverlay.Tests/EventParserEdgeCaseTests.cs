using System;
using ArcRaidersOverlay;
using Xunit;

namespace ArcRaidersOverlay.Tests;

public class EventParserEdgeCaseTests
{
    #region Parse Edge Cases

    [Fact]
    public void Parse_NullInput_ReturnsEmptyList()
    {
        var events = EventParser.Parse(null!);

        Assert.Empty(events);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyList()
    {
        var events = EventParser.Parse("");

        Assert.Empty(events);
    }

    [Fact]
    public void Parse_WhitespaceOnly_ReturnsEmptyList()
    {
        var events = EventParser.Parse("   \n\t  ");

        Assert.Empty(events);
    }

    [Fact]
    public void Parse_MultipleEvents_ReturnsAll()
    {
        var text = "Supply Drop - Dread Canyon - 5:32\nConvoy - Blackstone Quarry - 2:15";

        var events = EventParser.Parse(text);

        Assert.Equal(2, events.Count);
        Assert.Equal("Supply Drop", events[0].Name);
        Assert.Equal("Convoy", events[1].Name);
    }

    [Fact]
    public void Parse_ActiveEvent_HandlesActiveKeyword()
    {
        var text = "Extraction - Wraith Basin - ACTIVE";

        var events = EventParser.Parse(text);

        Assert.Single(events);
        Assert.Equal("ACTIVE", events[0].Timer);
    }

    [Fact]
    public void Parse_MixedCaseTimer_NormalizesToUppercase()
    {
        var text = "Storm - Dread Canyon - active";

        var events = EventParser.Parse(text);

        Assert.Single(events);
        Assert.Equal("ACTIVE", events[0].Timer);
    }

    [Fact]
    public void Parse_ExtraWhitespace_TrimsCorrectly()
    {
        var text = "  Supply Drop   -   Dread Canyon   -   5:32  ";

        var events = EventParser.Parse(text);

        Assert.Single(events);
        Assert.Equal("Supply Drop", events[0].Name);
        Assert.Equal("Dread Canyon", events[0].Location);
    }

    [Fact]
    public void Parse_NoMatchingPattern_ExtractsTimerOnly()
    {
        var text = "Some random text with a timer 3:45 in it";

        var events = EventParser.Parse(text);

        Assert.Single(events);
        Assert.Equal("Unknown Event", events[0].Name);
        Assert.Equal("3:45", events[0].Timer);
    }

    [Fact]
    public void Parse_NoTimerAtAll_ReturnsEmptyList()
    {
        var text = "No timer here at all";

        var events = EventParser.Parse(text);

        Assert.Empty(events);
    }

    #endregion

    #region ParseTimer Edge Cases

    [Fact]
    public void ParseTimer_NullInput_ReturnsNull()
    {
        var result = EventParser.ParseTimer(null!);

        Assert.Null(result);
    }

    [Fact]
    public void ParseTimer_EmptyString_ReturnsNull()
    {
        var result = EventParser.ParseTimer("");

        Assert.Null(result);
    }

    [Fact]
    public void ParseTimer_InvalidFormat_ReturnsNull()
    {
        var result = EventParser.ParseTimer("invalid");

        Assert.Null(result);
    }

    [Fact]
    public void ParseTimer_SingleDigitMinutes_Parses()
    {
        var result = EventParser.ParseTimer("1:30");

        Assert.NotNull(result);
        Assert.Equal(new TimeSpan(0, 1, 30), result);
    }

    [Fact]
    public void ParseTimer_DoubleDigitMinutes_Parses()
    {
        var result = EventParser.ParseTimer("12:45");

        Assert.NotNull(result);
        Assert.Equal(new TimeSpan(0, 12, 45), result);
    }

    [Fact]
    public void ParseTimer_ZeroMinutes_Parses()
    {
        var result = EventParser.ParseTimer("0:30");

        Assert.NotNull(result);
        Assert.Equal(new TimeSpan(0, 0, 30), result);
    }

    [Fact]
    public void ParseTimer_ActiveMixedCase_ReturnsZero()
    {
        var result = EventParser.ParseTimer("Active");

        Assert.NotNull(result);
        Assert.Equal(TimeSpan.Zero, result);
    }

    [Fact]
    public void ParseTimer_WithSurroundingText_ExtractsTimer()
    {
        var result = EventParser.ParseTimer("timer is 5:30 remaining");

        Assert.NotNull(result);
        Assert.Equal(new TimeSpan(0, 5, 30), result);
    }

    #endregion

    #region DetectMap Edge Cases

    [Fact]
    public void DetectMap_NullInput_ReturnsNull()
    {
        var result = EventParser.DetectMap(null!);

        Assert.Null(result);
    }

    [Fact]
    public void DetectMap_EmptyString_ReturnsNull()
    {
        var result = EventParser.DetectMap("");

        Assert.Null(result);
    }

    [Fact]
    public void DetectMap_UnknownLocation_ReturnsNull()
    {
        var result = EventParser.DetectMap("Unknown Place That Doesnt Exist");

        Assert.Null(result);
    }

    [Fact]
    public void DetectMap_CaseInsensitive_Matches()
    {
        var result = EventParser.DetectMap("DREAD CANYON");

        Assert.Equal("Dread Canyon", result);
    }

    [Fact]
    public void DetectMap_PartialMatch_Matches()
    {
        var result = EventParser.DetectMap("Event at dread canyon area");

        Assert.Equal("Dread Canyon", result);
    }

    #endregion
}
