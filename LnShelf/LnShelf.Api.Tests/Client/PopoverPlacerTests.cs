using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The popover's edge-flipping, asserted without a browser. This is the reason the
/// arithmetic lives in a plain class instead of inside the component.
/// </summary>
public sealed class PopoverPlacerTests
{
    private const double MenuWidth = 208;
    private const double MenuHeight = 250;

    private static TileRect Tile(double left, double top, double width = 92, double height = 132) => new()
    {
        Left = left,
        Top = top,
        Right = left + width,
        Bottom = top + height,
        ViewportWidth = 1440,
        ViewportHeight = 900,
    };

    [Fact]
    public void MidGridTileHangsBelowItsLeftEdge()
    {
        var placement = PopoverPlacer.For(Tile(left: 400, top: 300), MenuWidth, MenuHeight);

        placement.Left.ShouldBe(400);
        placement.Top.ShouldBe(438);      // tile bottom (432) plus the 6px gap
        placement.FlippedHorizontally.ShouldBeFalse();
        placement.FlippedVertically.ShouldBeFalse();
    }

    [Fact]
    public void TileAtTheRightEdgeFlipsToAlignWithItsRightEdge()
    {
        // 1330 + 208 + 6 overruns a 1440 viewport, so the menu anchors to the tile's
        // right edge instead of its left.
        var placement = PopoverPlacer.For(Tile(left: 1330, top: 300), MenuWidth, MenuHeight);

        placement.FlippedHorizontally.ShouldBeTrue();
        placement.Left.ShouldBe(1330 + 92 - MenuWidth);
        (placement.Left + MenuWidth).ShouldBeLessThanOrEqualTo(1440);
    }

    [Fact]
    public void TileNearTheBottomFlipsAboveItself()
    {
        var placement = PopoverPlacer.For(Tile(left: 400, top: 700), MenuWidth, MenuHeight);

        placement.FlippedVertically.ShouldBeTrue();
        placement.Top.ShouldBe(700 - MenuHeight - 6);
    }

    [Fact]
    public void BottomRightTileFlipsOnBothAxesAtOnce()
    {
        var placement = PopoverPlacer.For(Tile(left: 1330, top: 700), MenuWidth, MenuHeight);

        placement.FlippedHorizontally.ShouldBeTrue();
        placement.FlippedVertically.ShouldBeTrue();
        (placement.Left + MenuWidth).ShouldBeLessThanOrEqualTo(1440);
        (placement.Top + MenuHeight).ShouldBeLessThanOrEqualTo(900);
    }

    [Fact]
    public void AMenuWithNowhereToFlipIsPinnedRatherThanPlacedOffScreen()
    {
        // A viewport shorter than the menu: flipping above would put the top at a
        // negative offset, which would hide the read rows entirely.
        var tile = Tile(left: 10, top: 180) with { ViewportHeight = 300 };

        var placement = PopoverPlacer.For(tile, MenuWidth, MenuHeight);

        placement.Top.ShouldBe(6);
        placement.Left.ShouldBe(10);
    }
}
