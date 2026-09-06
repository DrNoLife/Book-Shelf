namespace LnShelf.Api.Features.Shelf.Data;

/// <summary>
/// Builds the development fixture: the 24 series of <c>docs/seed-library.json</c>,
/// whose counts reproduce the approved mockup's header numbers exactly
/// (256 read, 299 owned, 489 published, 43 backlog, 190 to buy).
/// </summary>
// Pure factories, no DbContext: the tests assert the mockup's numbers against
// the very graph that gets persisted.
public static class ShelfSeedData
{
    /// <summary>The one series with a hand-built volume breakdown.</summary>
    public const string ExplicitVolumeTitle = "The Saga of Tanya the Evil";

    /// <summary>
    /// All release dates are generated relative to a fixed day rather than to
    /// "now", so the fixture is reproducible. It is a fixture, not synced data:
    /// once real dates arrive from MangaUpdates this whole file goes away.
    /// </summary>
    private static readonly DateOnly ReleaseAnchor = new(2026, 7, 29);

    /// <summary>Days between generated volumes, roughly a real LN cadence.</summary>
    private const int ReleaseIntervalDays = 120;

    /// <summary>The newest released volume landed this long before the anchor.</summary>
    private const int NewestReleaseOffsetDays = 60;

    /// <summary>
    /// How many series at the tail of the file get unreleased and announced
    /// volumes, so the volume grid has ghost tiles to render next session.
    /// </summary>
    private const int GhostTailSeriesCount = 3;

    public static IReadOnlyList<Series> CreateLibrary(SeedLibraryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var library = new List<Series>(document.Series.Count);
        var firstGhostIndex = document.Series.Count - GhostTailSeriesCount;

        for (var index = 0; index < document.Series.Count; index++)
        {
            var entry = document.Series[index];
            library.Add(entry.Volumes is null
                ? CreateFromCounts(entry, withGhostVolumes: index >= firstGhostIndex)
                : CreateFromExplicitBreakdown(entry));
        }

        return library;
    }

    /// <summary>
    /// 8 read, volume 9 reading, 11 owned, 14 out — 3 to buy, 3 in the backlog.
    /// The series-detail reference, so its volumes are spelled out rather than
    /// generated.
    /// </summary>
    public static Series CreateTanyaSeries()
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),

            // No external ids: this is a local fixture, not a synced series.
            AniListId = null,
            MangaUpdatesId = null,

            Format = Format.LightNovel,
            TitleEnglish = ExplicitVolumeTitle,
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
                UserState = CreateTanyaVolumeUserState(number),
            });
        }

        return series;
    }

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
    /// Volumes 12-17 get no row at all — absence is Unread and NotOwned, and
    /// leaving them out exercises the projection's null handling.
    /// </summary>
    private static VolumeUserState? CreateTanyaVolumeUserState(int number) => number switch
    {
        // Read and owned. Only the most recent read carries a date; the fixture
        // does not pretend to know when the older volumes were finished.
        <= 8 => new VolumeUserState
        {
            ReadStatus = ReadStatus.Read,
            ReadAt = number == 8 ? new DateOnly(2026, 7, 6) : null,
            Owned = true,
            OwnedFormat = Shared.Shelf.OwnedFormat.Physical,
            PurchasePrice = TanyaPriceFor(number),
        },

        // Currently reading, so owned and still in the backlog.
        9 => new VolumeUserState
        {
            ReadStatus = ReadStatus.Reading,
            Owned = true,
            OwnedFormat = Shared.Shelf.OwnedFormat.Physical,
            PurchasePrice = TanyaPriceFor(number),
        },

        // Bought, not started.
        10 or 11 => new VolumeUserState
        {
            ReadStatus = ReadStatus.Unread,
            Owned = true,
            OwnedFormat = Shared.Shelf.OwnedFormat.Physical,
            PurchasePrice = TanyaPriceFor(number),
        },

        _ => null,
    };

    // Vols 1-3 hardcover, the rest paperback — 3×180 + 8×111 = 1 428 kr over 11
    // volumes, the spend line the mockup prints on the series page.
    private static decimal TanyaPriceFor(int number) => number <= 3 ? 180m : 111m;

    private static Series CreateFromExplicitBreakdown(SeedSeries entry)
    {
        if (!string.Equals(entry.TitleEnglish, ExplicitVolumeTitle, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"'{entry.TitleEnglish}' carries a volumes breakdown but has no hand-built builder. " +
                "Either add one or remove the volumes field so it is generated from counts.");
        }

        return CreateTanyaSeries();
    }

    /// <summary>
    /// The file's generation rule: volumes 1..published, of which 1..read are
    /// read, read+1..owned are owned and unread, and the rest are released and
    /// unowned.
    /// </summary>
    private static Series CreateFromCounts(SeedSeries entry, bool withGhostVolumes)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = null,
            MangaUpdatesId = null,
            Format = Enum.Parse<Format>(entry.Format),
            TitleEnglish = entry.TitleEnglish,
            TitleRomaji = entry.TitleRomaji,
            TitleNative = entry.TitleNative,
            PublicationStatus = Enum.Parse<PublicationStatus>(entry.Status),
            Publisher = entry.Publisher,
            StartYear = entry.StartYear,
            Genres = entry.Genres?.ToArray() ?? [],
            Author = entry.Author,
            Illustrator = entry.Illustrator,
            UserState = new SeriesUserState
            {
                // The file specifies no tracking status. Everything on the shelf
                // is being read: the alternatives would mean inventing per-series
                // state the fixture does not describe.
                TrackingStatus = TrackingStatus.Reading,
                Notes = entry.Notes,
                HasNewVolume = entry.NewVolume,
            },
        };

        for (var number = 1; number <= entry.Published; number++)
        {
            series.Volumes.Add(new Volume
            {
                Id = Guid.CreateVersion7(),
                Number = number,
                ReleaseDate = ReleaseDateFor(number, entry.Published),
                UserState = CreateVolumeUserStateFromCounts(number, entry),
            });
        }

        if (withGhostVolumes)
        {
            AddGhostVolumes(series, entry.Published);
        }

        return series;
    }

    private static VolumeUserState? CreateVolumeUserStateFromCounts(int number, SeedSeries entry)
    {
        if (number <= entry.Read)
        {
            return new VolumeUserState
            {
                ReadStatus = ReadStatus.Read,
                Owned = true,
                OwnedFormat = Shared.Shelf.OwnedFormat.Physical,
            };
        }

        if (number <= entry.Owned)
        {
            return new VolumeUserState
            {
                ReadStatus = ReadStatus.Unread,
                Owned = true,
                OwnedFormat = Shared.Shelf.OwnedFormat.Physical,
            };
        }

        // Released and unowned: no row, which is the to-buy pile.
        return null;
    }

    /// <summary>Two dated future volumes and one announced with no date.</summary>
    private static void AddGhostVolumes(Series series, int published)
    {
        series.Volumes.Add(new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = published + 1,
            ReleaseDate = ReleaseAnchor.AddDays(45),
        });
        series.Volumes.Add(new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = published + 2,
            ReleaseDate = ReleaseAnchor.AddDays(135),
        });
        series.Volumes.Add(new Volume
        {
            Id = Guid.CreateVersion7(),
            Number = published + 3,
            ReleaseDate = null,
        });
    }

    private static DateOnly ReleaseDateFor(int number, int published) =>
        ReleaseAnchor.AddDays(-NewestReleaseOffsetDays - ((published - number) * ReleaseIntervalDays));
}
