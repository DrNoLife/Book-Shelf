using LnShelf.Client.Components;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The tick strip's cap. Asserted without a browser, which is the reason the split
/// lives in a plain record rather than inside the component.
/// </summary>
public sealed class VolumeTickStripLayoutTests
{
    private const int Cap = 24;

    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    [InlineData(24)]
    public void For_UpToTheCap_DrawsEveryTickAndNoTail(int volumeCount)
    {
        var layout = VolumeTickStripLayout.For(Ticks(volumeCount), Cap);

        layout.Visible.Count.ShouldBe(volumeCount);
        layout.HiddenCount.ShouldBe(0);
        layout.HiddenLabel.ShouldBeNull();
    }

    [Fact]
    public void For_PastTheCap_DrawsTheFirstCapTicksAndCountsTheRest()
    {
        // Berserk's shape: 42 volumes in one table cell.
        var layout = VolumeTickStripLayout.For(Ticks(42), Cap);

        layout.Visible.Count.ShouldBe(Cap);
        layout.Visible[0].Number.ShouldBe(1);
        layout.Visible[^1].Number.ShouldBe(24);

        // The tail names what it stands for, because a bare "+18" says nothing about
        // which end of the series it swallowed.
        layout.HiddenCount.ShouldBe(18);
        layout.HiddenLabel.ShouldBe("18 more — volumes 25–42");
    }

    [Fact]
    public void For_NoVolumes_DrawsNothing()
    {
        var layout = VolumeTickStripLayout.For([], Cap);

        layout.Visible.ShouldBeEmpty();
        layout.HiddenCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void For_ACapThatCannotDrawAnything_Throws(int cap) =>
        Should.Throw<ArgumentOutOfRangeException>(() => VolumeTickStripLayout.For(Ticks(3), cap));

    /// <summary>
    /// The four kinds, one after the other, so a layout test also exercises the kind
    /// derivation the strip renders from.
    /// </summary>
    public static TheoryData<bool, bool, ReadStatus, VolumeTickKind> KindMatrix => new()
    {
        // isOut  owned  readStatus            kind
        { true,   true,  ReadStatus.Read,      VolumeTickKind.Read },
        { true,   false, ReadStatus.Read,      VolumeTickKind.Read },
        { true,   true,  ReadStatus.Unread,    VolumeTickKind.OwnedUnread },

        // Reading has no tick of its own: a 4px tick cannot carry a sixth state, so it
        // collapses into whichever of the two "in hand or not" kinds it actually is.
        { true,   true,  ReadStatus.Reading,   VolumeTickKind.OwnedUnread },
        { true,   false, ReadStatus.Unread,    VolumeTickKind.ToBuy },
        { true,   false, ReadStatus.Reading,   VolumeTickKind.ToBuy },
        { false,  false, ReadStatus.Unread,    VolumeTickKind.NotOut },
    };

    [Theory]
    [MemberData(nameof(KindMatrix))]
    public void Kind_ForEveryVolumeShape_IsTheAppearanceTheStripDraws(
        bool isOut,
        bool owned,
        ReadStatus readStatus,
        VolumeTickKind expected)
    {
        var tick = new VolumeTickResponse
        {
            Number = 1,
            IsOut = isOut,
            Owned = owned,
            ReadStatus = readStatus,
        };

        tick.Kind.ShouldBe(expected);
    }

    private static IReadOnlyList<VolumeTickResponse> Ticks(int volumeCount) =>
    [
        .. Enumerable.Range(1, volumeCount).Select(number => new VolumeTickResponse
        {
            Number = number,
            IsOut = true,
            Owned = true,
            ReadStatus = ReadStatus.Read,
        }),
    ];
}
