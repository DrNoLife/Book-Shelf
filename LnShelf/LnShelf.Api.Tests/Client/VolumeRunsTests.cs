using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The arithmetic behind "Mark 1–N read". Pulled out of the volume section for exactly
/// this reason, the same move <c>PopoverPlacer</c> already models.
/// </summary>
public sealed class VolumeRunsTests
{
    [Fact]
    public void NextRunEnd_NothingRead_OffersTheFirstVolume() =>
        VolumeRuns.NextRunEnd(Shelf(released: 5, read: 0), IsRead).ShouldBe(1);

    [Fact]
    public void NextRunEnd_AnUnbrokenRun_OffersOnePastIt() =>
        VolumeRuns.NextRunEnd(Shelf(released: 5, read: 3), IsRead).ShouldBe(4);

    [Fact]
    public void NextRunEnd_EverythingOutIsRead_OffersNothing() =>
        VolumeRuns.NextRunEnd(Shelf(released: 5, read: 5), IsRead).ShouldBeNull();

    /// <summary>
    /// The cap that matters: a series with 5 in print and 12 announced must never offer
    /// to mark a volume nobody can own read.
    /// </summary>
    [Fact]
    public void NextRunEnd_VolumesNotOutYet_AreNeverOffered() =>
        VolumeRuns.NextRunEnd(Shelf(released: 5, read: 5, notOut: 7), IsRead).ShouldBeNull();

    [Fact]
    public void NextRunEnd_NothingIsOut_OffersNothing() =>
        VolumeRuns.NextRunEnd(Shelf(released: 0, read: 0, notOut: 3), IsRead).ShouldBeNull();

    /// <summary>
    /// The run is the *leading* one: reading volume 5 out of order does not make
    /// "mark 1–6 read" the sensible offer.
    /// </summary>
    [Fact]
    public void NextRunEnd_AGapInTheRun_StopsAtTheGap()
    {
        var volumes = new[]
        {
            Volume(1, isOut: true, isRead: true),
            Volume(2, isOut: true, isRead: false),
            Volume(3, isOut: true, isRead: true),
        };

        VolumeRuns.NextRunEnd(volumes, IsRead).ShouldBe(2);
    }

    /// <summary>Volumes arrive in whatever order; the run is by number, not by position.</summary>
    [Fact]
    public void NextRunEnd_VolumesOutOfOrder_AreCountedByNumber()
    {
        var volumes = new[]
        {
            Volume(3, isOut: true, isRead: false),
            Volume(1, isOut: true, isRead: true),
            Volume(2, isOut: true, isRead: true),
        };

        VolumeRuns.NextRunEnd(volumes, IsRead).ShouldBe(3);
    }

    /// <summary>
    /// Whether a select-mode selection can go through the "1–N" bulk endpoint. Selecting 10
    /// and 12 under a "Mark 2 read" label used to send "through 12" and mark twelve.
    /// </summary>
    [Theory]
    [InlineData(new[] { 1, 2, 3 }, 3)]
    [InlineData(new[] { 1 }, 1)]
    [InlineData(new[] { 3, 1, 2 }, 3)]
    [InlineData(new[] { 10, 12 }, null)]
    [InlineData(new[] { 1, 3 }, null)]
    [InlineData(new[] { 2, 3 }, null)]
    [InlineData(new int[0], null)]
    public void SelectionAsLeadingRun_TheSelection_IsARunFromOneOrNothing(int[] selectedNumbers, int? expected)
    {
        var volumes = Shelf(released: 10, read: 0, notOut: 2);
        var selected = volumes
            .Where(volume => selectedNumbers.Contains(volume.Number))
            .Select(volume => volume.VolumeId)
            .ToHashSet();

        VolumeRuns.SelectionAsLeadingRun(volumes, selected).ShouldBe(expected);
    }

    private static bool IsRead(VolumeResponse volume) => volume.ReadStatus is ReadStatus.Read;

    private static IReadOnlyList<VolumeResponse> Shelf(int released, int read, int notOut = 0) =>
    [
        .. Enumerable.Range(1, released).Select(number => Volume(number, isOut: true, isRead: number <= read)),
        .. Enumerable.Range(released + 1, notOut).Select(number => Volume(number, isOut: false, isRead: false)),
    ];

    private static VolumeResponse Volume(int number, bool isOut, bool isRead) => new()
    {
        VolumeId = Guid.CreateVersion7(),
        Number = number,
        IsOut = isOut,
        ReadStatus = isRead ? ReadStatus.Read : ReadStatus.Unread,
        Owned = false,
        IsLocalOnly = false,
    };
}
