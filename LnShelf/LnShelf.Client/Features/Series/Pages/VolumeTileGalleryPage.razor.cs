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

    private sealed record TileSample(
        string Label,
        VolumeResponse Volume,
        bool IsSelectMode = false,
        bool IsSelected = false);
}
