namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The chapter-range rows the sync records, and what the series page says about them.
/// </summary>
public sealed class SyncDiscrepanciesTests
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 14, 3, 0, 0, TimeSpan.Zero);

    // Written from Wikipedia: settled, and the page says Wikipedia's range is the one shown.
    [Fact]
    public void ChapterRange_ASourceWasWritten_IsResolvedByIt()
    {
        var discrepancy = SyncDiscrepancies.ChapterRange(Series(), Disagreement(MetadataSourceName.Wikipedia));

        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.Wikipedia);
        discrepancy.Note.ShouldNotBeNull().ShouldContain("Wikipedia's range");

        var conflict = SourceConflictMapper.ToConflict(new RecordedDisagreement(discrepancy, RecordedAt), choice: null);
        conflict.DisplayedSource.ShouldBe(MetadataSourceName.Wikipedia);
    }

    // Nothing was written for the volume, so no source may be named as the one shown.
    [Fact]
    public void ChapterRange_NoSourceWasWritten_IsUnresolvedAndSaysNoRangeIsShown()
    {
        var discrepancy = SyncDiscrepancies.ChapterRange(Series(), Disagreement(writtenSource: null));

        discrepancy.ResolvedWith.ShouldBeNull();
        discrepancy.Note.ShouldNotBeNull().ShouldContain("no range");

        var conflict = SourceConflictMapper.ToConflict(new RecordedDisagreement(discrepancy, RecordedAt), choice: null);
        conflict.ShouldSatisfyAllConditions(
            () => conflict.DisplayedSource.ShouldBeNull(),
            () => conflict.Options.ShouldAllBe(option => !option.IsDisplayed),
            () => conflict.Explanation.ShouldStartWith("No range is shown"),
            () => conflict.CanChoose.ShouldBeFalse());
    }

    private static ChapterStartDisagreement Disagreement(MetadataSourceName? writtenSource) => new(
        24,
        [
            new SourcedVolumeRange(MetadataSourceName.Wikipedia, new VolumeChapterRange(24, 223m, null)),
            new SourcedVolumeRange(MetadataSourceName.MangaDex, new VolumeChapterRange(24, 230m, null)),
        ],
        writtenSource);

    private static Series Series() => new()
    {
        Id = Guid.CreateVersion7(),
        Format = Format.Manga,
        TitleEnglish = "Chainsaw Man",
        PublicationStatus = PublicationStatus.Ongoing,
    };
}
