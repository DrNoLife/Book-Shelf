using LnShelf.Client.Features.Sync;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The review list's words for each question the last sync left, and which questions it lists.
/// </summary>
public sealed class ReviewTextTests
{
    [Fact]
    public void ToReview_ARunWithSettledAndOpenQuestions_ListsOnlyTheOpenOnes()
    {
        var open = Discrepancy(SyncDiscrepancyField.SeriesLink);
        var run = Run(open, Discrepancy(SyncDiscrepancyField.VolumeCount, resolvedWith: MetadataSourceName.MangaUpdates));

        run.ToReview.ShouldHaveSingleItem().ShouldBe(open);
    }

    [Theory]
    [InlineData(SyncDiscrepancyField.SeriesLink, null, "Not linked to a release source yet")]
    [InlineData(SyncDiscrepancyField.VolumeCount, null, "Sources disagree on the volume count")]
    [InlineData(SyncDiscrepancyField.Status, null, "Sources disagree on publication status")]
    [InlineData(SyncDiscrepancyField.ReleaseDate, 8, "Vol 8: sources disagree on the release date")]
    public void Question_EveryField_ReadsAsTheSeriesPageWordsIt(
        SyncDiscrepancyField field, int? volumeNumber, string expected) =>
        Discrepancy(field, volumeNumber).Question.ShouldBe(expected);

    // The sources by their display names, never the enum's "AppleBooks".
    [Fact]
    public void Said_ADisagreement_NamesEachSourceAsAPersonWould() =>
        (Discrepancy(SyncDiscrepancyField.ReleaseDate, 8) with
        {
            Values =
            [
                new SourcedValueResponse { Source = MetadataSourceName.MangaUpdates, Value = "in print" },
                new SourcedValueResponse { Source = MetadataSourceName.AppleBooks, Value = null },
            ],
        }).Said.ShouldBe("MangaUpdates says in print. Apple Books says nothing.");

    [Fact]
    public void Said_TheLinkQuestion_PointsAtTheSeriesPage() =>
        Discrepancy(SyncDiscrepancyField.SeriesLink).Said.ShouldBe("Find it on MangaUpdates from the series page.");

    private static SyncDiscrepancyResponse Discrepancy(
        SyncDiscrepancyField field,
        int? volumeNumber = null,
        MetadataSourceName? resolvedWith = null) =>
        new()
        {
            SeriesId = Guid.CreateVersion7(),
            SeriesTitle = "86―EIGHTY-SIX",
            Field = field,
            VolumeNumber = volumeNumber,
            Values = [],
            ResolvedWith = resolvedWith,
        };

    private static SyncSummaryResponse Run(params SyncDiscrepancyResponse[] discrepancies) => new()
    {
        RunId = Guid.CreateVersion7(),
        StartedAt = DateTimeOffset.UnixEpoch,
        FinishedAt = DateTimeOffset.UnixEpoch,
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
        Failures = 0,
        Summary = "refreshed",
        Discrepancies = discrepancies,
    };
}
