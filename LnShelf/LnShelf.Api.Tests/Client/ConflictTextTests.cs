using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// The words the series page uses for a disagreement: human field names, and the volume named
/// when the disagreement is about one.
/// </summary>
// The page used to print the contract's enum name ("Sources disagree on ReleaseDate") and left
// the volume out of a per-volume heading, which is what this pins down (review finding).
public sealed class ConflictTextTests
{
    [Theory]
    [InlineData(SyncDiscrepancyField.VolumeCount, "the volume count")]
    [InlineData(SyncDiscrepancyField.ReleaseDate, "the release date")]
    [InlineData(SyncDiscrepancyField.Status, "publication status")]
    [InlineData(SyncDiscrepancyField.SeriesLink, "which series this is")]
    public void FieldName_EveryField_IsAHumanPhrase(SyncDiscrepancyField field, string expected) =>
        field.FieldName.ShouldBe(expected);

    [Theory]
    [InlineData(SyncDiscrepancyField.ReleaseDate, 15, "Sources disagree on Vol 15's release date")]
    [InlineData(SyncDiscrepancyField.VolumeCount, null, "Sources disagree on the volume count")]
    [InlineData(SyncDiscrepancyField.Status, null, "Sources disagree on publication status")]
    [InlineData(SyncDiscrepancyField.SeriesLink, null, "Not linked to a release source yet")]
    public void Heading_NamesTheFieldAndTheVolume(SyncDiscrepancyField field, int? volumeNumber, string expected) =>
        Conflict(field, volumeNumber).Heading.ShouldBe(expected);

    [Fact]
    public void Heading_NeverPrintsTheEnumName()
    {
        foreach (var field in Enum.GetValues<SyncDiscrepancyField>())
        {
            Conflict(field, field is SyncDiscrepancyField.ReleaseDate ? 4 : null).Heading
                .ShouldNotContain(field.ToString(), Case.Sensitive);
        }
    }

    [Fact]
    public void Sentence_ReadsInTheMockupsVoice()
    {
        var conflict = Conflict(
            SyncDiscrepancyField.ReleaseDate,
            15,
            Option(MetadataSourceName.MangaUpdates, "in print"),
            Option(MetadataSourceName.AppleBooks, "9 Mar 2027"));

        conflict.Sentence.ShouldBe("MangaUpdates says it's in print. Apple Books says 9 Mar 2027.");
    }

    [Fact]
    public void Sentence_ASeriesLink_SaysItIsWaitingOnYou() =>
        Conflict(SyncDiscrepancyField.SeriesLink, null).Sentence.ShouldBe("Waiting on you.");

    [Theory]
    [InlineData(SyncDiscrepancyField.ReleaseDate, 23, "Vol 23's release date")]
    [InlineData(SyncDiscrepancyField.ReleaseDate, null, "Release date")]
    [InlineData(SyncDiscrepancyField.VolumeCount, null, "Volume count")]
    [InlineData(SyncDiscrepancyField.Status, null, "Publication status")]
    [InlineData(SyncDiscrepancyField.SeriesLink, null, "Not linked to a release source yet")]
    public void Subject_DropsTheListTitlesPrefix(SyncDiscrepancyField field, int? volumeNumber, string expected) =>
        Conflict(field, volumeNumber).Subject.ShouldBe(expected);

    [Fact]
    public void Subject_ABonusVolume_KeepsItsFraction() =>
        (Conflict(SyncDiscrepancyField.ReleaseDate, null) with { VolumeNumber = 7.5m })
            .Subject.ShouldBe("Vol 7.5's release date");

    [Theory]
    [InlineData(SourceDisplayReason.YourChoice, "using Apple Books (your choice)")]
    [InlineData(SourceDisplayReason.Automatic, "using Apple Books (automatic)")]
    public void SettledNote_ASettledDisagreement_SaysWhoSettledIt(SourceDisplayReason reason, string expected) =>
        Settled(SyncDiscrepancyField.ReleaseDate, 15, reason, MetadataSourceName.AppleBooks)
            .SettledNote.ShouldBe(expected);

    [Fact]
    public void SettledNote_AnOpenDisagreement_IsNull() =>
        (Conflict(SyncDiscrepancyField.ReleaseDate, 15) with { DisplayedSource = MetadataSourceName.MangaUpdates })
            .SettledNote.ShouldBeNull();

    [Fact]
    public void SettledNote_SettledWithNothingShown_IsNull() =>
        (Conflict(SyncDiscrepancyField.VolumeCount, null) with { NeedsReview = false })
            .SettledNote.ShouldBeNull();

    [Fact]
    public void RowHeading_AnOpenDisagreement_IsTheSubject() =>
        Conflict(SyncDiscrepancyField.ReleaseDate, 23).RowHeading.ShouldBe("Vol 23's release date");

    [Theory]
    [InlineData(SourceDisplayReason.Automatic, MetadataSourceName.AniList, "Volume count · using AniList (automatic)")]
    [InlineData(SourceDisplayReason.YourChoice, MetadataSourceName.MangaUpdates, "Volume count · using MangaUpdates (your choice)")]
    public void RowHeading_ASettledDisagreement_AddsWhoSettledIt(
        SourceDisplayReason reason, MetadataSourceName source, string expected) =>
        Settled(SyncDiscrepancyField.VolumeCount, null, reason, source).RowHeading.ShouldBe(expected);

    [Theory]
    [InlineData(1, "Sources disagree · 1")]
    [InlineData(3, "Sources disagree · 3")]
    public void ListTitle_CountsTheRows(int count, string expected) =>
        ConflictText.ListTitle(count).ShouldBe(expected);

    [Theory]
    [InlineData(MetadataSourceName.AppleBooks, "Using Apple Books (your choice)")]
    [InlineData(MetadataSourceName.OpenLibrary, "Using Open Library (your choice)")]
    [InlineData(MetadataSourceName.MangaUpdates, "Using MangaUpdates (your choice)")]
    [InlineData(MetadataSourceName.AniList, "Using AniList (your choice)")]
    public void ChoiceMarker_NamesTheSourceTheWayAPersonWouldSayIt(MetadataSourceName source, string expected) =>
        ConflictText.ChoiceMarker(source).ShouldBe(expected);

    [Fact]
    public void ChoiceFor_FindsTheChoiceForTheFieldAndTheVolume()
    {
        var series = SeriesWithChoices(
            new SourceChoiceResponse
            {
                Field = SyncDiscrepancyField.ReleaseDate,
                VolumeNumber = 8,
                Source = MetadataSourceName.AppleBooks,
            },
            new SourceChoiceResponse { Field = SyncDiscrepancyField.Status, Source = MetadataSourceName.MangaUpdates });

        series.ChoiceFor(SyncDiscrepancyField.ReleaseDate, 8).ShouldBe(MetadataSourceName.AppleBooks);
        series.ChoiceFor(SyncDiscrepancyField.ReleaseDate, 9).ShouldBeNull();
        series.ChoiceFor(SyncDiscrepancyField.Status).ShouldBe(MetadataSourceName.MangaUpdates);
        series.ChoiceFor(SyncDiscrepancyField.VolumeCount).ShouldBeNull();
    }

    private static SourceConflictResponse Conflict(
        SyncDiscrepancyField field,
        int? volumeNumber,
        params SourceOptionResponse[] options) => new()
        {
            Field = field,
            VolumeNumber = volumeNumber,
            Options = options,
            Reason = SourceDisplayReason.WaitingOnYou,
            Explanation = "Nothing was changed.",
            CanChoose = field is not SyncDiscrepancyField.SeriesLink,
            NeedsReview = true,
            RecordedAt = DateTimeOffset.UnixEpoch,
        };

    private static SourceConflictResponse Settled(
        SyncDiscrepancyField field,
        int? volumeNumber,
        SourceDisplayReason reason,
        MetadataSourceName source) =>
        Conflict(field, volumeNumber) with { NeedsReview = false, Reason = reason, DisplayedSource = source };

    private static SourceOptionResponse Option(MetadataSourceName source, string displayValue) => new()
    {
        Source = source,
        SourceName = source.DisplayName,
        DisplayValue = displayValue,
        IsDisplayed = false,
    };

    private static SeriesDetailResponse SeriesWithChoices(params SourceChoiceResponse[] choices) => new()
    {
        SeriesId = Guid.CreateVersion7(),
        TitleEnglish = "86",
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
        TrackingStatus = TrackingStatus.Reading,
        Genres = [],
        HasNewVolume = false,
        Counts = new SeriesCountsResponse
        {
            Read = 0,
            Owned = 0,
            Published = 0,
            ToBuy = 0,
            Backlog = 0,
            Tracked = 0,
            NotOut = 0,
            Dated = 0,
        },
        Volumes = [],
        SourceChoices = choices,
    };
}
