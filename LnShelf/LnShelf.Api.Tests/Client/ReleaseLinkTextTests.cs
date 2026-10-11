using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>The "Not linked" panel's words for a candidate and for what linking did.</summary>
public sealed class ReleaseLinkTextTests
{
    [Theory]
    [InlineData("Novel", 2017, "Novel · 2017")]
    [InlineData("Novel", null, "Novel")]
    [InlineData(null, 2017, "2017")]
    [InlineData(null, null, "")]
    public void Detail_WhatTheSourceGave_IsJoinedWithoutGaps(string? sourceType, int? startYear, string expected) =>
        Candidate(sourceType, startYear).Detail.ShouldBe(expected);

    [Fact]
    public void HeldBy_ARecordAnotherSeriesHolds_NamesThatSeries() =>
        (Candidate("Novel", 2017) with { LinkedSeriesId = Guid.CreateVersion7(), LinkedSeriesTitle = "86 (manga)" })
            .HeldBy.ShouldBe("Already linked to 86 (manga) on your shelf");

    [Fact]
    public void HeldBy_AFreeRecord_IsNull() => Candidate("Novel", 2017).HeldBy.ShouldBeNull();

    // An eleven-digit id, printed whole and without a culture's digit grouping.
    [Fact]
    public void SourcePageUrl_ALongId_IsTheMangaUpdatesPage() =>
        ReleaseLinkText.SourcePageUrl(45348350266).ShouldBe("https://www.mangaupdates.com/series.html?id=45348350266");

    [Theory]
    [InlineData(true, "Linked. The volume list is refreshed from MangaUpdates.")]
    [InlineData(false, "Linked. The volume list follows at the next sync.")]
    public void Outcome_SaysWhetherTheVolumesFollowedAtOnce(bool refreshed, string expected) =>
        ReleaseLinkText.Outcome(new ReleaseLinkResponse { Series = null!, Refreshed = refreshed }).ShouldBe(expected);

    private static ReleaseLinkCandidateResponse Candidate(string? sourceType, int? startYear) => new()
    {
        MangaUpdatesId = 17992364412,
        Title = "86",
        SourceType = sourceType,
        StartYear = startYear,
    };
}
