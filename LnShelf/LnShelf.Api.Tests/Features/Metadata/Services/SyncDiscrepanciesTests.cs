namespace LnShelf.Api.Tests.Features.Metadata.Services;

/// <summary>
/// The disagreement rows the sync records: which source each side names, which source settled
/// it, and, for chapter ranges, what the series page says about them.
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

    [Fact]
    public void SeriesLink_AnUnmatchedSeries_IsUnresolvedAndCarriesBothSides()
    {
        var series = Series();
        var match = new ReleaseSourceMatch
        {
            Outcome = ReleaseMatchOutcome.LowConfidence,
            Score = 61,
            Explanation = "Closest title was too far off.",
        };

        var discrepancy = SyncDiscrepancies.SeriesLink(series, match);

        // A question for a person, so no source is named as the answer.
        ShouldBeAbout(discrepancy, series, SyncDiscrepancyField.SeriesLink);
        discrepancy.ResolvedWith.ShouldBeNull();
        discrepancy.Note.ShouldBe("LowConfidence: Closest title was too far off.");
        ValuesOf(discrepancy).ShouldBe(
        [
            (MetadataSourceName.AniList, "Chainsaw Man"),
            (MetadataSourceName.MangaUpdates, "best match scored 61"),
        ]);
    }

    [Theory]
    // More volumes counted than tracked: they are created, so MangaUpdates is what was acted on.
    [InlineData(20, 18, MetadataSourceName.MangaUpdates)]
    // Fewer: nothing is deleted, so the shelf's own list stands.
    [InlineData(18, 20, MetadataSourceName.AniList)]
    public void VolumeCount_NoChoice_IsResolvedByTheSourceTheWriterActedOn(
        int sourceCount, int trackedCount, MetadataSourceName expected)
    {
        var series = Series();

        var discrepancy = SyncDiscrepancies.VolumeCount(series, Info(), sourceCount, trackedCount, choice: null);

        ShouldBeAbout(discrepancy, series, SyncDiscrepancyField.VolumeCount);
        discrepancy.ResolvedWith.ShouldBe(expected);
        discrepancy.ResolvedByChoice.ShouldBeFalse();
        discrepancy.Note.ShouldBe("MangaUpdates status: 20 Volumes (Ongoing)");
        ValuesOf(discrepancy).ShouldBe(
        [
            (MetadataSourceName.AniList, trackedCount.ToString(CultureInfo.InvariantCulture)),
            (MetadataSourceName.MangaUpdates, sourceCount.ToString(CultureInfo.InvariantCulture)),
        ]);
    }

    [Fact]
    public void VolumeCount_AUserChoice_IsResolvedByTheChosenSourceAndStillRecorded()
    {
        var series = Series();

        var discrepancy = SyncDiscrepancies.VolumeCount(
            series, Info(), sourceCount: 20, trackedCount: 18, Choice(SyncDiscrepancyField.VolumeCount, MetadataSourceName.AniList));

        // The choice overrules the precedence answer (MangaUpdates, for a higher count), and the
        // disagreement is still a row: choosing did not make it go away.
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.AniList);
        discrepancy.ResolvedByChoice.ShouldBeTrue();
        ValuesOf(discrepancy).Count.ShouldBe(2);
    }

    [Fact]
    public void Status_NoChoice_IsUnresolvedAndSetsCompletedAgainstTheSourceStatus()
    {
        var series = Series();

        var discrepancy = SyncDiscrepancies.Status(series, Info(PublicationStatus.Hiatus), choice: null);

        ShouldBeAbout(discrepancy, series, SyncDiscrepancyField.Status);
        discrepancy.ResolvedWith.ShouldBeNull();
        discrepancy.ResolvedByChoice.ShouldBeFalse();
        ValuesOf(discrepancy).ShouldBe(
        [
            (MetadataSourceName.AniList, "Completed"),
            (MetadataSourceName.MangaUpdates, "Hiatus"),
        ]);
    }

    [Fact]
    public void Status_AUserChoice_IsResolvedByIt()
    {
        var discrepancy = SyncDiscrepancies.Status(
            Series(), Info(PublicationStatus.Ongoing), Choice(SyncDiscrepancyField.Status, MetadataSourceName.MangaUpdates));

        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.MangaUpdates);
        discrepancy.ResolvedByChoice.ShouldBeTrue();
    }

    [Fact]
    public void StatusFromChoice_TheDisplacedValue_IsTheOtherSideRatherThanCompleted()
    {
        var series = Series();

        var discrepancy = SyncDiscrepancies.StatusFromChoice(
            series,
            Info(PublicationStatus.Cancelled),
            Choice(SyncDiscrepancyField.Status, MetadataSourceName.MangaUpdates),
            replacedValue: "Ongoing");

        ShouldBeAbout(discrepancy, series, SyncDiscrepancyField.Status);
        discrepancy.ResolvedWith.ShouldBe(MetadataSourceName.MangaUpdates);
        discrepancy.ResolvedByChoice.ShouldBeTrue();
        ValuesOf(discrepancy).ShouldBe(
        [
            (MetadataSourceName.AniList, "Ongoing"),
            (MetadataSourceName.MangaUpdates, "Cancelled"),
        ]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReleaseDate_AFutureDateOnAVolumeInPrint_SetsInPrintAgainstTheSourceDate(bool chosen)
    {
        var series = Series();
        var volume = new Volume { Id = Guid.CreateVersion7(), Number = 7.5m, IsReleased = true };
        var choice = chosen ? Choice(SyncDiscrepancyField.ReleaseDate, MetadataSourceName.OpenLibrary) : null;

        var discrepancy = SyncDiscrepancies.ReleaseDate(
            series, volume, MetadataSourceName.OpenLibrary, new DateOnly(2027, 2, 16), "Nothing was written.", choice);

        ShouldBeAbout(discrepancy, series, SyncDiscrepancyField.ReleaseDate);
        discrepancy.VolumeNumber.ShouldBe(7.5m);
        discrepancy.Note.ShouldBe("Nothing was written.");
        discrepancy.ResolvedWith.ShouldBe(choice?.Source);
        discrepancy.ResolvedByChoice.ShouldBe(chosen);
        ValuesOf(discrepancy).ShouldBe(
        [
            (MetadataSourceName.MangaUpdates, DiscrepancyValueFormat.InPrint),
            (MetadataSourceName.OpenLibrary, DiscrepancyValueFormat.Date(new DateOnly(2027, 2, 16))),
        ]);

        // A record, never a write: building the row leaves the volume as it was.
        volume.ReleaseDate.ShouldBeNull();
        volume.IsReleased.ShouldBeTrue();
    }

    private static void ShouldBeAbout(SyncDiscrepancy discrepancy, Series series, SyncDiscrepancyField field)
    {
        discrepancy.Id.ShouldNotBe(Guid.Empty);
        discrepancy.SeriesId.ShouldBe(series.Id);
        discrepancy.SeriesTitle.ShouldBe(series.TitleEnglish);
        discrepancy.Field.ShouldBe(field);
    }

    private static List<(MetadataSourceName Source, string? Value)> ValuesOf(SyncDiscrepancy discrepancy) =>
        [.. discrepancy.Values.Select(value => (value.Source, value.Value))];

    private static ReleaseSeriesInfo Info(PublicationStatus status = PublicationStatus.Ongoing) => new()
    {
        ExternalId = 75336092483,
        Title = "Chainsaw Man",
        PublicationStatus = status,
        RawStatus = "20 Volumes (Ongoing)",
    };

    private static SourceChoice Choice(SyncDiscrepancyField field, MetadataSourceName source) => new()
    {
        Id = Guid.CreateVersion7(),
        Field = field,
        Source = source,
    };

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
