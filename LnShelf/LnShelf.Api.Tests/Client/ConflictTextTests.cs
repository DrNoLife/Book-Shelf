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
    [InlineData(SourceDisplayReason.YourChoice, "Using Apple Books (your choice)")]
    [InlineData(SourceDisplayReason.Automatic, "Using Apple Books (automatic)")]
    public void SettledLine_SaysWhoSettledIt(SourceDisplayReason reason, string expected) =>
        (Conflict(SyncDiscrepancyField.ReleaseDate, 15) with
        {
            Reason = reason,
            DisplayedSource = MetadataSourceName.AppleBooks,
        }).SettledLine.ShouldBe(expected);

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
