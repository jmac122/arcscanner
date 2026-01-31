using ArcRaidersOverlay;
using Xunit;

namespace ArcRaidersOverlay.Tests;

public class DataManagerTests
{
    [Fact]
    public void GetItem_ExactMatch_ReturnsItem()
    {
        var manager = new DataManager();

        var item = manager.GetItem("Scrap Metal");

        Assert.NotNull(item);
        Assert.Equal("Scrap Metal", item!.Name);
    }

    [Fact]
    public void GetItem_NormalizedMatch_ReturnsItem()
    {
        var manager = new DataManager();

        var item = manager.GetItem("scrap-metal");

        Assert.NotNull(item);
        Assert.Equal("Scrap Metal", item!.Name);
    }

    [Fact]
    public void GetItem_Unknown_ReturnsNull()
    {
        var manager = new DataManager();

        var item = manager.GetItem("Definitely Not An Item");

        Assert.Null(item);
    }
}
