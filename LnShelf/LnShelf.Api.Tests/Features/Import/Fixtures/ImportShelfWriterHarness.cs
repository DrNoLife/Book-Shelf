using Microsoft.EntityFrameworkCore;

namespace LnShelf.Api.Tests.Features.Import.Fixtures;

/// <summary>
/// The pieces every <see cref="ImportShelfWriter"/> test needs: an in-memory shelf, a
/// metadata source that answers for one series, and a group to apply to it.
/// </summary>
// Shared rather than repeated, because the writer's rules are several classes' worth of
// tests — volume state, dates, subtitles — and three copies of the same builders is
// three chances for them to drift apart.
internal static class ImportShelfWriterHarness
{
    public const int AniListId = 85976;

    /// <summary>The date every built row carries unless the test says otherwise.</summary>
    public static readonly DateOnly DefaultReadAt = new(2022, 3, 12);

    public static readonly DateTimeOffset AppliedAt = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);

    public static LnShelfDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<LnShelfDbContext>()
            .UseInMemoryDatabase($"import-{Guid.CreateVersion7()}")
            .Options, TestUsers.Current);

    public static ImportShelfWriter CreateWriter(
        LnShelfDbContext dbContext,
        ISeriesMetadataSource metadataSource) =>
        new(dbContext, metadataSource, Substitute.For<ICoverCache>(),
            TestUsers.Limits(dbContext), NullLogger<ImportShelfWriter>.Instance);

    public static ISeriesMetadataSource CreateMetadataSource(int? volumeCount)
    {
        var metadataSource = Substitute.For<ISeriesMetadataSource>();
        metadataSource.GetSeriesAsync(AniListId, Arg.Any<CancellationToken>())
            .Returns(new SeriesMetadata
            {
                ExternalId = AniListId,
                TitleEnglish = "Overlord",
                Format = Format.LightNovel,
                PublicationStatus = PublicationStatus.Ongoing,
                Volumes = volumeCount is null
                    ? []
                    : [.. Enumerable.Range(1, volumeCount.Value).Select(number => new VolumeMetadata { Number = number })],
            });

        return metadataSource;
    }

    /// <param name="readAt">Null means the default date, not the absence of one.</param>
    public static ImportGroup CreateGroup(
        int[] volumeNumbers,
        ReadStatus readStatus = ReadStatus.Read,
        bool treatReadAsOwned = true,
        string? isbn = null,
        string? subtitle = null,
        DateOnly? readAt = null)
    {
        ArgumentNullException.ThrowIfNull(volumeNumbers);

        var group = new ImportGroup
        {
            Id = Guid.CreateVersion7(),
            SeriesTitle = "Overlord",
            SeriesKey = "overlord",
            Status = ImportGroupStatus.Resolved,
            MatchedAniListId = AniListId,
            TreatReadAsOwned = treatReadAsOwned,
        };

        foreach (var volumeNumber in volumeNumbers)
        {
            group.Rows.Add(new ImportRow
            {
                Id = Guid.CreateVersion7(),
                LineNumber = volumeNumber,
                RawTitle = $"Overlord, Vol. {volumeNumber}",
                VolumeNumber = volumeNumber,
                Subtitle = subtitle,
                ReadStatus = readStatus,
                OwnedInExport = false,
                OwnedFormat = OwnedFormat.Digital,
                ReadAt = readAt ?? DefaultReadAt,
                Isbn = isbn,
            });
        }

        return group;
    }

    public static Task<Series> LoadSeriesAsync(LnShelfDbContext dbContext) =>
        dbContext.Series
            .Include(series => series.Volumes).ThenInclude(volume => volume.UserState)
            .Include(series => series.UserState)
            .SingleAsync();

    public static Task<Volume> LoadVolumeAsync(LnShelfDbContext dbContext, int number) =>
        dbContext.Volumes
            .Include(volume => volume.UserState)
            .SingleAsync(volume => volume.Number == number);
}
