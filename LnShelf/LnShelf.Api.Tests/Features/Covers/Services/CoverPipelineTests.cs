using LnShelf.Api.Features.Covers.Services;
using LnShelf.Api.Features.Shelf.Abstractions;
using LnShelf.Api.Tests.Features.Covers.Fixtures;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;
using LnShelf.Api.Tests.Features.Metadata.Services;
using LnShelf.Shared.Covers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace LnShelf.Api.Tests.Features.Covers.Services;

/// <summary>
/// Uploaded and shared covers through the app's real wiring (SyncPipelineHost): the services
/// resolved from the container, covers in a real folder, only the wire and Postgres replaced.
/// </summary>
public sealed class CoverPipelineTests
{
    private const long WrongRecord = 1;
    private static readonly Guid Owner = TestUsers.Owner;
    private static readonly Guid ReaderOfTheWrongRecord = TestUsers.SomeoneElse;
    private static readonly Guid ReaderOfTheRightRecord = CommunityCoverShelf.Third;

    // The matcher linked the series to the wrong record and the owner shared a cover before
    // fixing the link by hand. The shared row has to follow the link, or readers of the wrong
    // record keep being offered another series' cover.
    [Fact]
    public async Task LinkAsync_ASharedCoverOnARelinkedSeries_IsOfferedForTheNewRecordAndNoLongerForTheOld()
    {
        await using var host = Host();
        var volumeId = await SeedSeriesAsync(host, Owner, aniListId: 105398, WrongRecord);
        var readerOfTheWrongRecordsVolume = await SeedSeriesAsync(host, ReaderOfTheWrongRecord, aniListId: 1, WrongRecord);
        var readerOfTheRightRecordsVolume = await SeedSeriesAsync(
            host, ReaderOfTheRightRecord, aniListId: 2, MangaUpdatesFixtures.SoloLevelingSeriesId);

        await using (var scope = host.ScopeAs(Owner))
        {
            await scope.ServiceProvider.GetRequiredService<ICommunityCoverCommandService>()
                .SetSharingAsync(true, CancellationToken.None);
            await scope.ServiceProvider.GetRequiredService<IVolumeCoverCommandService>()
                .SetCoverAsync(volumeId, CommunityCoverShelf.CreatePng(200, 300), CancellationToken.None);
        }

        await using (var scope = host.ScopeAs(Owner))
        {
            var seriesId = await scope.ServiceProvider.GetRequiredService<LnShelfDbContext>().Volumes
                .Where(volume => volume.Id == volumeId)
                .Select(volume => volume.SeriesId)
                .SingleAsync();

            await scope.ServiceProvider.GetRequiredService<IReleaseLinkCommandService>().LinkAsync(
                seriesId,
                new LinkReleaseSourceRequest { MangaUpdatesId = MangaUpdatesFixtures.SoloLevelingSeriesId },
                CancellationToken.None);
        }

        (await CandidatesAsync(host, ReaderOfTheWrongRecord, readerOfTheWrongRecordsVolume)).ShouldBeEmpty();
        (await CandidatesAsync(host, ReaderOfTheRightRecord, readerOfTheRightRecordsVolume)).ShouldHaveSingleItem();
    }

    // A replaced cover whose delete failed sits under a volume that is still on the shelf, so
    // the volume check alone never found it.
    [Fact]
    public async Task RunAsync_ALeftoverUploadOnALiveVolume_IsSweptAndTheCoverInUseKept()
    {
        await using var host = Host();
        var volumeId = await SeedSeriesAsync(host, Owner, aniListId: 105398, WrongRecord);
        var leftover = CoverObjectKey.ForVolumeOverride(volumeId, Guid.CreateVersion7(), CoverNormalizer.Extension);
        string current;

        await using (var scope = host.ScopeAs(Owner))
        {
            var detail = await scope.ServiceProvider.GetRequiredService<IVolumeCoverCommandService>()
                .SetCoverAsync(volumeId, CommunityCoverShelf.CreatePng(200, 300), CancellationToken.None);
            current = detail.ShouldNotBeNull().Volumes.Single(volume => volume.VolumeId == volumeId)
                .CoverObjectKey.ShouldNotBeNull();

            var png = CommunityCoverShelf.CreatePng(20, 30);
            await scope.ServiceProvider.GetRequiredService<ICoverStore>()
                .SaveAsync(leftover, png, "image/png", CancellationToken.None);
        }

        // Both written before the grace period began, so only the reference decides.
        var longAgo = DateTime.UtcNow - CoverOrphanSweeper.UnreferencedUploadGracePeriod - TimeSpan.FromMinutes(5);
        File.SetLastWriteTimeUtc(PathOf(host, current), longAgo);
        File.SetLastWriteTimeUtc(PathOf(host, leftover), longAgo);

        CoverOrphanSweepReport report;
        await using (var scope = host.ScopeAs(Owner))
        {
            report = await scope.ServiceProvider.GetRequiredService<CoverOrphanSweeper>()
                .RunAsync(dryRun: false, CancellationToken.None);
        }

        report.Deleted.ShouldBe(1);
        host.StoreHolds(leftover).ShouldBeFalse();
        host.StoreHolds(current).ShouldBeTrue();
    }

    private static SyncPipelineHost Host() =>
        new(AppleBooksFixtures.SoloLevelingSearch, MangaUpdatesFixtures.SoloLevelingSeries);

    private static string PathOf(SyncPipelineHost host, string objectKey) =>
        Path.Combine(host.CoverRoot, objectKey.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>A user (the sweep visits users) with a one-volume manga series; returns the volume.</summary>
    private static async Task<Guid> SeedSeriesAsync(SyncPipelineHost host, Guid userId, int aniListId, long mangaUpdatesId)
    {
        await using var scope = host.ScopeAs(userId);
        var dbContext = scope.ServiceProvider.GetRequiredService<LnShelfDbContext>();
        var volumeId = Guid.CreateVersion7();

        dbContext.Users.Add(new User { Id = userId, Issuer = "test", Subject = $"sub-{userId}", Email = $"{userId}@example.com" });
        dbContext.Series.Add(new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = aniListId,
            MangaUpdatesId = mangaUpdatesId,
            Format = Format.Manga,
            TitleEnglish = "Solo Leveling",
            PublicationStatus = PublicationStatus.Completed,
            Description = "A hunter levels up.",
            Volumes = [new Volume { Id = volumeId, Number = 1, IsReleased = true }],
        });
        await dbContext.SaveChangesAsync();

        return volumeId;
    }

    private static async Task<IReadOnlyList<CommunityCoverCandidate>> CandidatesAsync(
        SyncPipelineHost host,
        Guid userId,
        Guid volumeId)
    {
        await using var scope = host.ScopeAs(userId);
        var response = await scope.ServiceProvider.GetRequiredService<ICommunityCoverQueryService>()
            .ListForVolumeAsync(volumeId, CancellationToken.None);

        return response.ShouldNotBeNull().Candidates;
    }
}
