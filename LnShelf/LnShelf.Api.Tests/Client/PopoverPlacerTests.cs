using LnShelf.Client.Features.Series;
using LnShelf.Client.Features.Series.Components;

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
    public void AMenuCarryingABlurbIsPlacedAtItsOwnTallerSize()
    {
        // The placement is computed before the menu renders, so its size arrives as the
        // component's nominal numbers rather than as a measurement. A blurb makes the menu
        // taller, and a tile that had room below it for the short menu may not have room
        // for this one — which is the whole reason the size is asked for per volume.
        var tile = Tile(left: 400, top: 480);

        var plain = PopoverPlacer.For(tile, VolumePopover.Width, VolumePopover.Height);
        var described = PopoverPlacer.For(
            tile, VolumePopover.DescribedWidth, VolumePopover.DescribedHeight);

        plain.FlippedVertically.ShouldBeFalse();     // 612 + 6 + 250 fits in 900
        described.FlippedVertically.ShouldBeTrue();  // 612 + 6 + 372 does not
        described.Top.ShouldBe(480 - VolumePopover.DescribedHeight - 6);
    }

    [Fact]
    public void TheBlurbSizesAreAskedForPerVolume()
    {
        var withBlurb = Volume(description: "A blurb the source actually supplied.");
        var without = Volume(description: null);

        VolumePopover.WidthFor(withBlurb).ShouldBe(VolumePopover.DescribedWidth);
        VolumePopover.HeightFor(withBlurb).ShouldBe(VolumePopover.DescribedHeight);
        VolumePopover.WidthFor(without).ShouldBe(VolumePopover.Width);
        VolumePopover.HeightFor(without).ShouldBe(VolumePopover.Height);
    }

    [Fact]
    public void TheRowsOnlySomeVolumesHaveAreCountedIntoTheHeight()
    {
        // An owned volume carries the edition buttons and one with an override carries
        // "Remove cover". Both were missing from the nominal height, which is how the menu
        // came to hang off the bottom of a phone screen with its last row below the fold.
        var plain = Volume(description: null);
        var owned = plain with { Owned = true };
        var overridden = plain with { HasCoverOverride = true };

        VolumePopover.HeightFor(plain).ShouldBe(VolumePopover.Height);
        VolumePopover.HeightFor(owned).ShouldBeGreaterThan(VolumePopover.HeightFor(plain));
        VolumePopover.HeightFor(overridden).ShouldBeGreaterThan(VolumePopover.HeightFor(plain));
        VolumePopover.HeightFor(owned with { HasCoverOverride = true })
            .ShouldBeGreaterThan(VolumePopover.HeightFor(owned));
    }

    [Theory]
    [InlineData(16, 100)]     // hangs below
    [InlineData(16, 500)]     // flips above
    [InlineData(232, 100)]    // right column, hangs below
    [InlineData(232, 500)]    // right column, flips above
    public void TheTallestMenuStaysInsideAPhoneScreen(double left, double top)
    {
        // 400x850, and the widest, tallest menu the component can render: a blurb, the
        // edition buttons and "Remove cover". The nominal numbers are what the placement
        // is computed from, so they are what has to fit — this is the assertion that was
        // missing when they said 372 and the menu rendered 403.
        var volume = Volume(description: "A blurb the source actually supplied.") with
        {
            Owned = true,
            HasCoverOverride = true,
        };

        var tile = Tile(left, top, width: 96, height: 144) with
        {
            ViewportWidth = 400,
            ViewportHeight = 850,
        };

        var placement = PopoverPlacer.For(
            tile, VolumePopover.WidthFor(volume), VolumePopover.HeightFor(volume));

        placement.Left.ShouldBeGreaterThanOrEqualTo(0);
        placement.Top.ShouldBeGreaterThanOrEqualTo(0);
        (placement.Left + VolumePopover.WidthFor(volume)).ShouldBeLessThanOrEqualTo(400);
        (placement.Top + VolumePopover.HeightFor(volume)).ShouldBeLessThanOrEqualTo(850);
    }

    private static VolumeResponse Volume(string? description) => new()
    {
        VolumeId = Guid.CreateVersion7(),
        Number = 1,
        Description = description,
        IsOut = true,
        ReadStatus = ReadStatus.Unread,
        Owned = false,
        IsLocalOnly = false,
    };

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
