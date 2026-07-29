namespace LnShelf.Api.Features.Shelf.Data;

/// <summary>
/// The walking skeleton's fixture: one series whose state matches the approved
/// mockup (docs/design/) so the numbers on screen can be checked by eye.
/// </summary>
// A pure factory rather than logic inside the seeder, so the tests can assert
// the mockup's counts against the very graph that gets persisted.
public static class ShelfSeedData
{
    /// <summary>Volumes 1-14 are out, 15 and 16 are dated, 17 is announced with no date.</summary>
    private static readonly (int Number, DateOnly? ReleaseDate)[] TanyaVolumes =
    [
        (1, new DateOnly(2016, 8, 23)),
        (2, new DateOnly(2016, 12, 20)),
        (3, new DateOnly(2017, 4, 18)),
        (4, new DateOnly(2017, 8, 22)),
        (5, new DateOnly(2017, 12, 19)),
        (6, new DateOnly(2018, 4, 17)),
        (7, new DateOnly(2018, 8, 21)),
        (8, new DateOnly(2018, 12, 18)),
        (9, new DateOnly(2019, 6, 18)),
        (10, new DateOnly(2019, 12, 17)),
        (11, new DateOnly(2020, 8, 18)),
        (12, new DateOnly(2021, 6, 22)),
        (13, new DateOnly(2022, 6, 21)),
        (14, new DateOnly(2026, 7, 25)),
        (15, new DateOnly(2026, 9, 12)),
        (16, new DateOnly(2026, 12, 5)),
        (17, null),
    ];

    /// <summary>
    /// 8 read, volume 9 reading, 11 owned, 14 out — which makes 3 to buy
    /// (12-14), 3 in the backlog (9-11) and the series behind.
    /// </summary>
    public static Series CreateTanyaSeries()
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),

            // No external ids: this is a local fixture, not a synced series.
            // Phase 1.2's AniList search is what supplies real ids.
            AniListId = null,
            MangaUpdatesId = null,

            Format = Format.LightNovel,
            TitleEnglish = "The Saga of Tanya the Evil",
            TitleRomaji = "Yōjo Senki",
            TitleNative = "幼女戦記",
            PublicationStatus = PublicationStatus.Ongoing,
            Publisher = "Kadokawa",
            StartYear = 2013,
            Genres = ["Military", "Isekai", "Fantasy", "Drama", "Historical"],
            Author = "Carlo Zen",
            Illustrator = "Shinobu Shinotsuki",
            UserState = new SeriesUserState
            {
                TrackingStatus = TrackingStatus.Reading,
                Notes = "Wait for vol 16 — current arc is mid. Vols 1–3 are hardcover, rest paperback.",
                StartedAt = new DateOnly(2023, 3, 12),
                LastReadAt = new DateOnly(2026, 7, 6),
                HasNewVolume = true,
            },
        };

        foreach (var (number, releaseDate) in TanyaVolumes)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                ReleaseDate = releaseDate,
                UserState = CreateVolumeUserState(number),
            });
        }

        return series;
    }

    /// <summary>
    /// Volumes 12-17 get no row at all — absence is Unread and NotOwned, and
    /// leaving them out exercises the projection's null handling.
    /// </summary>
    private static VolumeUserState? CreateVolumeUserState(int number) => number switch
    {
        // Read and owned. Only the most recent read carries a date; the fixture
        // does not pretend to know when the older volumes were finished.
        <= 8 => new VolumeUserState
        {
            ReadStatus = ReadStatus.Read,
            ReadAt = number == 8 ? new DateOnly(2026, 7, 6) : null,
            Owned = true,
            OwnedFormat = Shared.Shelf.OwnedFormat.Physical,
            PurchasePrice = PriceFor(number),
        },

        // Currently reading, so owned and still in the backlog.
        9 => new VolumeUserState
        {
            ReadStatus = ReadStatus.Reading,
            Owned = true,
            OwnedFormat = Shared.Shelf.OwnedFormat.Physical,
            PurchasePrice = PriceFor(number),
        },

        // Bought, not started.
        10 or 11 => new VolumeUserState
        {
            ReadStatus = ReadStatus.Unread,
            Owned = true,
            OwnedFormat = Shared.Shelf.OwnedFormat.Physical,
            PurchasePrice = PriceFor(number),
        },

        _ => null,
    };

    // Vols 1-3 hardcover, the rest paperback — 3×180 + 8×111 = 1 428 kr over 11
    // volumes, the spend line the mockup prints on the series page.
    private static decimal PriceFor(int number) => number <= 3 ? 180m : 111m;
}
