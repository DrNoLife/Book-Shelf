using System.Text.Json.Nodes;
using LnShelf.Api.Tests.Features.Metadata.Fixtures;

namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The sync through the app's real wiring, about what it asks the store and MangaUpdates and
/// what it may create from the answers: the extras cap, the shared night's allowance, the
/// cached MangaUpdates record and the sweep's daily rotation.
/// </summary>
// Pipeline-level on purpose (see SyncPipelineHost): each of these was a fault in how real
// pieces met — a writer and the count it was not handed, a budget and a cache the arbiter could
// not see, two services fetching one record, a fixed order and a shared allowance — which the
// unit tests beside them could not show.
//
// Solo Leveling because both of its sources were captured: MangaUpdates counts "13+2 Volumes"
// and the store answers Vols. 1 to 15.
public sealed class SyncPipelineCatalogueTests
{
    private const int MainVolumeCount = 13;

    [Fact]
    public async Task RunAsync_TheStoreListingMoreBonusVolumesThanExtrasLeft_CreatesOnlyTheOnesThereArePlacesFor()
    {
        // "13+2" with Vol. 7.5 already tracked leaves one place, and the store lists two more
        // bonus volumes. Before the cap the run created both, three extras where the note
        // grants two.
        await using var host = new SyncPipelineHost(
            AppleBooksFixtures.SoloLevelingSearch,
            MangaUpdatesFixtures.SoloLevelingSeries,
            editAppleBooks: json => WithBonusVolumes(json, "3.5", "5.5"));
        var series = SoloLeveling("Solo Leveling");
        series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = 7.5m, IsReleased = true });
        await host.SeedAsync(series);

        var run = await host.SyncAsync();

        run.Failures.ShouldBe(0);
        var saved = await host.LoadAsync(series.Id);
        saved.Volumes.Where(volume => volume.Number.IsBonusVolume)
            .Select(volume => volume.Number)
            .Order()
            .ShouldBe([3.5m, 7.5m]);
    }

    [Fact]
    public async Task RunAsync_TwoShelvesTrackingTheSameSeriesInOneNight_AskEachSourceOnceAndSpendOneRequest()
    {
        await using var host = new SyncPipelineHost(
            AppleBooksFixtures.SoloLevelingSearch, MangaUpdatesFixtures.SoloLevelingSeries);
        await host.SeedAsync(SoloLeveling("Solo Leveling"), TestUsers.Owner);
        await host.SeedAsync(SoloLeveling("Solo Leveling"), TestUsers.SomeoneElse);
        var night = new EnrichmentBudget(catalogueRequests: 10, isbnLookups: 10);

        await host.SyncAsync(night, TestUsers.Owner);
        await host.SyncAsync(night, TestUsers.SomeoneElse);

        // The second shelf's answers come from the cache, so they are not requests: the
        // allowance pays for the one search the store was sent, and MangaUpdates is asked for
        // the record once.
        host.AppleBooksRequests.Count.ShouldBe(1);
        night.Catalogue.Remaining.ShouldBe(9);
        host.MangaUpdatesSeriesRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RunAsync_AnAllowanceForOneSeries_GoesToADifferentSeriesTheNextNight()
    {
        // Two nights, each its own process (its own cache), with room for one store request.
        // In fixed title order the same series won it every night.
        var firstNight = await FirstSeriesAskedAboutOnAsync(new DateOnly(2026, 11, 7));
        var secondNight = await FirstSeriesAskedAboutOnAsync(new DateOnly(2026, 11, 8));

        firstNight.ShouldNotBe(secondNight);
    }

    private static async Task<string> FirstSeriesAskedAboutOnAsync(DateOnly night)
    {
        await using var host = new SyncPipelineHost(
            AppleBooksFixtures.SoloLevelingSearch,
            MangaUpdatesFixtures.SoloLevelingSeries,
            timeProvider: new FixedTimeProvider(new DateTimeOffset(night, new TimeOnly(4, 0), TimeSpan.Zero)));
        await host.SeedAsync(SoloLeveling("Solo Leveling"));
        await host.SeedAsync(SoloLeveling("Solo Leveling: Ragnarok"));

        await host.SyncAsync(new EnrichmentBudget(catalogueRequests: 1, isbnLookups: 0));

        return host.AppleBooksRequests.ShouldHaveSingleItem().Query;
    }

    /// <summary>
    /// The captured answer with bonus-volume rows added, each a copy of the Vol. 3 row under
    /// its own number and track id — a case the capture itself does not show.
    /// </summary>
    private static string WithBonusVolumes(string json, params string[] numbers)
    {
        var document = JsonNode.Parse(json)!.AsObject();
        var results = document["results"]!.AsArray();
        var template = results.First(result =>
            (string?)result!["trackName"] == "Solo Leveling, Vol. 3 (comic)")!;

        foreach (var number in numbers)
        {
            var row = template.DeepClone();
            row["trackName"] = $"Solo Leveling, Vol. {number} (comic)";
            row["trackId"] = 900_000_000 + results.Count;
            results.Add(row);
        }

        document["resultCount"] = results.Count;
        return document.ToJsonString();
    }

    private static Series SoloLeveling(string titleEnglish)
    {
        var series = new Series
        {
            Id = Guid.CreateVersion7(),
            AniListId = 105398,
            MangaUpdatesId = MangaUpdatesFixtures.SoloLevelingSeriesId,
            Format = Format.Manga,
            TitleEnglish = titleEnglish,
            PublicationStatus = PublicationStatus.Completed,

            // Set, so the description healer has no reason to ask AniList.
            Description = "A hunter levels up.",
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-1),
        };

        for (var number = 1; number <= MainVolumeCount; number++)
        {
            series.Volumes.Add(new Volume { Id = Guid.CreateVersion7(), Number = number, IsReleased = true });
        }

        return series;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
