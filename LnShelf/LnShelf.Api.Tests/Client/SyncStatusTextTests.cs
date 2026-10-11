using LnShelf.Client.Components;
using LnShelf.Client.Features.Sync;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The sidebar's sync note: a short relative headline, with what is waiting on the user
/// as the only second line.
/// </summary>
public sealed class SyncStatusTextTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "Synced just now")]
    [InlineData(5, "Synced 5 min ago")]
    [InlineData(180, "Synced 3 h ago")]
    [InlineData(60 * 24 * 3, "Synced 3 d ago")]
    public void Headline_FinishedRun_SaysHowLongAgo(int minutesAgo, string expected) =>
        CreateRun(finishedMinutesAgo: minutesAgo).Headline(Now).ShouldBe(expected);

    [Fact]
    public void Headline_RunStillGoing_SaysSo() =>
        CreateRun(finishedMinutesAgo: null, startedMinutesAgo: 10).Headline(Now).ShouldBe("Sync in progress");

    [Fact]
    public void Headline_UnfinishedRunFromLongAgo_DoesNotClaimToBeWorking() =>
        CreateRun(finishedMinutesAgo: null, startedMinutesAgo: 600).Headline(Now).ShouldBe("Last sync did not finish");

    [Fact]
    public void Attention_NothingToReview_IsNull() =>
        CreateRun(finishedMinutesAgo: 1).Attention.ShouldBeNull();

    [Fact]
    public void Attention_UnresolvedDiscrepancies_CountsOnlyThose() =>
        CreateRun(
                finishedMinutesAgo: 1,
                discrepancies: [CreateDiscrepancy(resolvedWith: null), CreateDiscrepancy(resolvedWith: MetadataSourceName.AniList)])
            .Attention.ShouldBe("1 to review");

    [Fact]
    public void Attention_FailuresAndReviews_ListsBothInOneLine() =>
        CreateRun(finishedMinutesAgo: 1, failures: 2, discrepancies: [CreateDiscrepancy(resolvedWith: null)])
            .Attention.ShouldBe("2 failed · 1 to review");

    [Fact]
    public void Attention_OnlyFailures_StillShowsThem() =>
        CreateRun(finishedMinutesAgo: 1, failures: 2).Attention.ShouldBe("2 failed");

    // The sidebar prints "to review" as a link of its own, so only the failures are text.
    [Theory]
    [InlineData(0, null)]
    [InlineData(2, " · 2 failed")]
    public void FailureClause_LeavesTheReviewCountToTheLink(int failures, string? expected) =>
        CreateRun(finishedMinutesAgo: 1, failures: failures, discrepancies: [CreateDiscrepancy(resolvedWith: null)])
            .FailureClause.ShouldBe(expected);

    [Fact]
    public void Tone_CleanFinishedRun_IsOk() =>
        CreateRun(finishedMinutesAgo: 1).Tone(Now).ShouldBe(SignalTone.Ok);

    [Fact]
    public void Tone_SomethingToReview_IsAttention() =>
        CreateRun(finishedMinutesAgo: 1, failures: 1).Tone(Now).ShouldBe(SignalTone.Attention);

    [Fact]
    public void Tone_RunStillGoing_IsQuiet() =>
        CreateRun(finishedMinutesAgo: null, startedMinutesAgo: 10).Tone(Now).ShouldBe(SignalTone.Quiet);

    [Fact]
    public void Tone_UnfinishedRunFromLongAgo_IsAttention() =>
        CreateRun(finishedMinutesAgo: null, startedMinutesAgo: 600).Tone(Now).ShouldBe(SignalTone.Attention);

    // run.Summary already ends with the failure count; Detail must not repeat it.
    [Fact]
    public void Detail_RunWithFailures_DoesNotRepeatTheFailureCount()
    {
        var detail = CreateRun(finishedMinutesAgo: 1, failures: 3).Detail;

        detail.ShouldEndWith("24 series refreshed");
        detail.ShouldNotContain("failed");
    }

    [Fact]
    public void NeverSynced_HasHeadlineAndDetail()
    {
        SyncStatusText.NeverSyncedHeadline.ShouldBe("Not synced yet");
        SyncStatusText.NeverSyncedDetail.ShouldNotBeNullOrWhiteSpace();
    }

    private static SyncSummaryResponse CreateRun(
        int? finishedMinutesAgo,
        int startedMinutesAgo = 0,
        int failures = 0,
        IReadOnlyList<SyncDiscrepancyResponse>? discrepancies = null) =>
        new()
        {
            RunId = Guid.CreateVersion7(),
            StartedAt = Now.AddMinutes(-(finishedMinutesAgo ?? startedMinutesAgo) - 1),
            FinishedAt = finishedMinutesAgo is { } ago ? Now.AddMinutes(-ago) : null,
            SeriesRefreshed = 0,
            SeriesLinked = 0,
            DatesMoved = 0,
            VolumesMarkedReleased = 0,
            VolumesEnriched = 0,
            VolumeCoversCached = 0,
            DescriptionsWritten = 0,
            VolumesResolvedBySearch = 0,
            EnrichmentGaps = 0,
            NewVolumes = 0,
            Failures = failures,
            Summary = "24 series refreshed",
            Discrepancies = discrepancies ?? [],
        };

    private static SyncDiscrepancyResponse CreateDiscrepancy(MetadataSourceName? resolvedWith) =>
        new()
        {
            SeriesId = Guid.CreateVersion7(),
            SeriesTitle = "Overlord",
            Field = SyncDiscrepancyField.SeriesLink,
            Values = [],
            ResolvedWith = resolvedWith,
        };
}
