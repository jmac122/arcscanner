using ArcRaidersOverlay;
using Xunit;

namespace ArcRaidersOverlay.Tests;

public class DataManagerEdgeCaseTests
{
    private readonly DataManager _manager;

    public DataManagerEdgeCaseTests()
    {
        _manager = new DataManager();
    }

    [Fact]
    public void GetItem_NullInput_ReturnsNull()
    {
        var item = _manager.GetItem(null!);

        Assert.Null(item);
    }

    [Fact]
    public void GetItem_EmptyString_ReturnsNull()
    {
        var item = _manager.GetItem("");

        Assert.Null(item);
    }

    [Fact]
    public void GetItem_WhitespaceOnly_ReturnsNull()
    {
        var item = _manager.GetItem("   ");

        Assert.Null(item);
    }

    [Fact]
    public void GetItem_CaseInsensitive_Matches()
    {
        var item = _manager.GetItem("SCRAP METAL");

        Assert.NotNull(item);
        Assert.Equal("Scrap Metal", item!.Name);
    }

    [Fact]
    public void GetItem_WithExtraSpaces_StillMatches()
    {
        var item = _manager.GetItem("  Scrap Metal  ");

        Assert.NotNull(item);
    }

    [Fact]
    public void GetItem_HyphenatedVariant_Matches()
    {
        var item = _manager.GetItem("scrap-metal");

        Assert.NotNull(item);
        Assert.Equal("Scrap Metal", item!.Name);
    }

    [Fact]
    public void GetItem_UnderscoreVariant_Matches()
    {
        var item = _manager.GetItem("scrap_metal");

        Assert.NotNull(item);
        Assert.Equal("Scrap Metal", item!.Name);
    }

    [Fact]
    public void GetItem_ValidItem_HasRequiredFields()
    {
        var item = _manager.GetItem("Scrap Metal");

        Assert.NotNull(item);
        Assert.False(string.IsNullOrEmpty(item!.Name));
        Assert.False(string.IsNullOrEmpty(item.Category));
    }

    [Fact]
    public void GetItem_SpecialCharactersInQuery_HandledGracefully()
    {
        // Should not throw, just return null for invalid input
        var item = _manager.GetItem("!@#$%^&*()");

        Assert.Null(item);
    }
}
