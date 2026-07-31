using LnShelf.Shared.Shelf;

namespace LnShelf.Client.Features.Series.Pages;

public partial class VolumeTileGalleryPage
{
    /// <summary>
    /// One sample per state, plus the two the rulings added: focus and selection.
    /// </summary>
    // Deliberately synthetic. Two of these states cannot be produced from the real
    // shelf — nothing has a release date, so nothing is "unreleased, dated", and
    // nothing is announced either — and writing fake volumes into the shelf to
    // photograph them would be worse than rendering the component directly.
    private static readonly IReadOnlyList<TileSample> Samples =
    [
        new("read", Volume(1, isOut: true, ReadStatus.Read, owned: true)),
        new("reading", Volume(2, isOut: true, ReadStatus.Reading, owned: true)),
        new("owned, unread", Volume(3, isOut: true, ReadStatus.Unread, owned: true)),
        new("to buy", Volume(4, isOut: true, ReadStatus.Unread, owned: false)),
        new("unreleased, dated", Volume(5, isOut: false, ReadStatus.Unread, owned: false,
            releaseDate: new DateOnly(2026, 9, 12))),
        new("announced, no date", Volume(6, isOut: false, ReadStatus.Unread, owned: false)),
        new("select mode", Volume(7, isOut: true, ReadStatus.Read, owned: true), IsSelectMode: true),
        new("selected", Volume(8, isOut: true, ReadStatus.Reading, owned: true),
            IsSelectMode: true, IsSelected: true),

        // The caption's optional volume title. No metadata source names volumes, so this
        // is the only place in the app where the title half of the caption is exercised.
        new("captioned title", Volume(9, isOut: true, ReadStatus.Read, owned: true),
            Title: "The Fool"),
    ];

    /// <summary>
    /// Tick strips. The first carries all four kinds, which no single series on the
    /// shelf does; the second is past the cap, so it ends in a "+N" tail.
    /// </summary>
    private static readonly IReadOnlyList<StripSample> Strips =
    [
        new("read 6 · owned-unread 3 · to buy 4 · not out 3", StripTicks(6, 3, 4, 3)),
        new("42 volumes, capped at 24", StripTicks(30, 12, 0, 0)),
    ];

    private static VolumeResponse Volume(
        int number,
        bool isOut,
        ReadStatus readStatus,
        bool owned,
        DateOnly? releaseDate = null) => new()
        {
            VolumeId = Guid.CreateVersion7(),
            Number = number,
            ReleaseDate = releaseDate,
            IsOut = isOut,
            ReadStatus = readStatus,
            Owned = owned,
            OwnedFormat = owned ? LnShelf.Shared.Shelf.OwnedFormat.Physical : null,
            IsLocalOnly = false,
        };

    /// <summary>Ticks in shelf order: read, then owned-unread, then to buy, then not out.</summary>
    private static IReadOnlyList<VolumeTickResponse> StripTicks(
        int read,
        int ownedUnread,
        int toBuy,
        int notOut)
    {
        var ticks = new List<VolumeTickResponse>();

        void Add(int count, bool isOut, bool owned, ReadStatus readStatus)
        {
            for (var index = 0; index < count; index++)
            {
                ticks.Add(new VolumeTickResponse
                {
                    Number = ticks.Count + 1,
                    IsOut = isOut,
                    Owned = owned,
                    ReadStatus = readStatus,
                });
            }
        }

        Add(read, isOut: true, owned: true, ReadStatus.Read);
        Add(ownedUnread, isOut: true, owned: true, ReadStatus.Unread);
        Add(toBuy, isOut: true, owned: false, ReadStatus.Unread);
        Add(notOut, isOut: false, owned: false, ReadStatus.Unread);

        return ticks;
    }

    private sealed record TileSample(
        string Label,
        VolumeResponse Volume,
        bool IsSelectMode = false,
        bool IsSelected = false,
        string? Title = null);

    private sealed record StripSample(string Label, IReadOnlyList<VolumeTickResponse> Ticks);
}
