using LnShelf.Api.Features.Recommendations.Models;
using LnShelf.Api.Features.Recommendations.Services;

namespace LnShelf.Api.Tests.Features.Recommendations.Services;

public sealed class SeedWeightsTests
{
    private static readonly DateOnly Today = new(2026, 10, 13);

    public static TheoryData<ShelfSeedFacts> NotSeeds => new()
    {
        Facts(status: TrackingStatus.PlanToStart, readVolumes: 3),
        Facts(status: TrackingStatus.Reading, readVolumes: 0, hasChapterProgress: false),
        Facts(status: TrackingStatus.Completed, readVolumes: 3, externalId: null),
    };

    [Theory]
    [MemberData(nameof(NotSeeds))]
    public void WeightOf_NotASeed_IsNull(ShelfSeedFacts facts) =>
        SeedWeights.WeightOf(facts, Today).ShouldBeNull();

    [Fact]
    public void WeightOf_CompletedOutweighsReadingAtTheSameDepthAndDate()
    {
        var completed = SeedWeights.WeightOf(Facts(status: TrackingStatus.Completed, readVolumes: 3, lastReadAt: Today), Today);
        var reading = SeedWeights.WeightOf(Facts(status: TrackingStatus.Reading, readVolumes: 3, lastReadAt: Today), Today);

        completed.ShouldNotBeNull().ShouldBeGreaterThan(reading.ShouldNotBeNull());
        completed.Value.ShouldBe(Math.Log2(4), 1e-9);
        reading.Value.ShouldBe(0.8 * Math.Log2(4), 1e-9);
    }

    [Fact]
    public void WeightOf_ReadAYearAgo_IsHalfOfReadToday()
    {
        var today = SeedWeights.WeightOf(Facts(readVolumes: 3, lastReadAt: Today), Today);
        var yearAgo = SeedWeights.WeightOf(Facts(readVolumes: 3, lastReadAt: Today.AddDays(-365)), Today);

        yearAgo.ShouldNotBeNull().ShouldBe(today.ShouldNotBeNull() / 2, 1e-9);
    }

    [Fact]
    public void WeightOf_Undated_CountsAsAYearOld()
    {
        var undated = SeedWeights.WeightOf(Facts(readVolumes: 3), Today);
        var yearAgo = SeedWeights.WeightOf(Facts(readVolumes: 3, lastReadAt: Today.AddDays(-365)), Today);

        undated.ShouldBe(yearAgo);
    }

    [Fact]
    public void WeightOf_FutureDate_CountsAsToday()
    {
        var future = SeedWeights.WeightOf(Facts(readVolumes: 3, lastReadAt: Today.AddDays(30)), Today);
        var today = SeedWeights.WeightOf(Facts(readVolumes: 3, lastReadAt: Today), Today);

        future.ShouldBe(today);
    }

    [Fact]
    public void WeightOf_ChapterOnlyReading_HasDepthOne()
    {
        var weight = SeedWeights.WeightOf(
            Facts(status: TrackingStatus.Reading, readVolumes: 0, hasChapterProgress: true, lastReadAt: Today), Today);

        weight.ShouldNotBeNull().ShouldBe(0.8, 1e-9);
    }

    [Fact]
    public void WeightOf_DepthIsCappedAtFifteenVolumes()
    {
        var fifteen = SeedWeights.WeightOf(Facts(readVolumes: 15, lastReadAt: Today), Today);
        var fifty = SeedWeights.WeightOf(Facts(readVolumes: 50, lastReadAt: Today), Today);

        fifty.ShouldBe(fifteen);
        fifteen.ShouldNotBeNull().ShouldBe(4.0, 1e-9);
    }

    [Fact]
    public void WeightOf_Dropped_IsNegative()
    {
        var weight = SeedWeights.WeightOf(
            Facts(status: TrackingStatus.Dropped, readVolumes: 9, lastReadAt: Today), Today);

        // Depth is ignored for a drop.
        weight.ShouldNotBeNull().ShouldBe(-0.5, 1e-9);
    }

    [Fact]
    public void WeightOf_DateFallsBackFromLastReadToVolumeReadToStarted()
    {
        var yearAgo = Today.AddDays(-365);
        var twoYearsAgo = Today.AddDays(-730);

        var lastRead = SeedWeights.WeightOf(Facts(readVolumes: 3, lastReadAt: Today, newestVolumeReadAt: yearAgo, startedAt: twoYearsAgo), Today);
        var volumeRead = SeedWeights.WeightOf(Facts(readVolumes: 3, newestVolumeReadAt: yearAgo, startedAt: twoYearsAgo), Today);
        var started = SeedWeights.WeightOf(Facts(readVolumes: 3, startedAt: twoYearsAgo), Today);

        var depth = Math.Log2(4);
        lastRead.ShouldNotBeNull().ShouldBe(depth, 1e-9);
        volumeRead.ShouldNotBeNull().ShouldBe(depth / 2, 1e-9);
        started.ShouldNotBeNull().ShouldBe(depth / 4, 1e-9);
    }

    [Fact]
    public void Choose_CapsPositiveAtFortyAndDroppedAtTen_StrongestFirst()
    {
        // Seed N has N volumes read, so a higher N is a heavier seed; dropped ones differ by age.
        var positives = Enumerable.Range(1, 45)
            .Select(number => Facts(externalId: number, readVolumes: number, lastReadAt: Today, seriesId: Guid.CreateVersion7()));
        var dropped = Enumerable.Range(1, 12)
            .Select(number => Facts(
                externalId: 100 + number,
                status: TrackingStatus.Dropped,
                lastReadAt: Today.AddDays(-30 * number),
                seriesId: Guid.CreateVersion7()));

        var seeds = SeedWeights.Choose([.. positives, .. dropped], Today);

        var chosenPositive = seeds.Where(seed => seed.Weight > 0).ToList();
        var chosenDropped = seeds.Where(seed => seed.Weight < 0).ToList();
        chosenPositive.Count.ShouldBe(40);
        chosenDropped.Count.ShouldBe(10);

        // Volumes 1-5 fall below the cap; the strongest (the deepest, capped at 15) lead.
        chosenPositive.Select(seed => seed.ExternalId).ShouldNotContain(1);
        chosenPositive.Select(seed => seed.Weight).ShouldBe(chosenPositive.Select(seed => seed.Weight).OrderDescending());

        // The most recently dropped is the most negative and comes first; the two oldest are cut.
        chosenDropped[0].ExternalId.ShouldBe(101);
        chosenDropped.Select(seed => seed.ExternalId).ShouldNotContain(112);
    }

    private static ShelfSeedFacts Facts(
        TrackingStatus status = TrackingStatus.Completed,
        int readVolumes = 0,
        bool hasChapterProgress = false,
        int? externalId = 1,
        DateOnly? lastReadAt = null,
        DateOnly? newestVolumeReadAt = null,
        DateOnly? startedAt = null,
        Guid? seriesId = null) =>
        new(seriesId ?? Guid.CreateVersion7(), externalId, "Series", Format.LightNovel, status,
            readVolumes, hasChapterProgress, lastReadAt, newestVolumeReadAt, startedAt);
}
