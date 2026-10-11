using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// "How is this counted?" (D11): the basis in plain words, the quoted text, and the reader's
/// own choice said first when it overrides the count.
/// </summary>
public sealed class CountProvenanceTextTests
{
    // Noon UTC, so the local date is the 11th wherever the test runs.
    private static readonly DateTimeOffset Checked = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(VolumeCountBasis.EnglishPublisherNote, "Yen Press: 14 Volumes (Ongoing)",
        "Counted from MangaUpdates, the English publisher's note: “Yen Press: 14 Volumes (Ongoing)”. Checked 11 Oct.")]
    [InlineData(VolumeCountBasis.MangaUpdatesStatus, "23 LN Volumes (Complete)",
        "Counted from MangaUpdates' status line, which counts the Japanese edition: “23 LN Volumes (Complete)”. No English publisher states a count. Checked 11 Oct.")]
    [InlineData(VolumeCountBasis.AniList, null,
        "Counted from AniList's volume list. MangaUpdates gives no count. Checked 11 Oct.")]
    public void CountExplanation_EachBasis_SaysWhereTheCountCameFrom(
        VolumeCountBasis basis, string? evidence, string expected) =>
        Series(basis, evidence, mangaUpdatesId: 1).CountExplanation.ShouldHaveSingleItem().ShouldBe(expected);

    // AddTimeReleaseLink keeps AniList's count when no MangaUpdates record was linked: then
    // MangaUpdates was never asked, which is not the same as it having no count.
    [Theory]
    [InlineData(null, "Counted from AniList's volume list. This series isn't linked to MangaUpdates yet. Checked 11 Oct.")]
    [InlineData(24348008396L, "Counted from AniList's volume list. MangaUpdates gives no count. Checked 11 Oct.")]
    public void CountExplanation_AniListBasis_DependsOnTheLink(long? mangaUpdatesId, string expected) =>
        Series(VolumeCountBasis.AniList, evidence: null, mangaUpdatesId).CountExplanation
            .ShouldHaveSingleItem().ShouldBe(expected);

    [Fact]
    public void CountExplanation_AHandMadeSeries_SaysSoWithoutACheckDate() =>
        Series(VolumeCountBasis.Manual, evidence: null, mangaUpdatesId: null).CountExplanation
            .ShouldHaveSingleItem().ShouldBe("Added by hand, so the volumes are the ones you added.");

    // Counted before provenance was recorded: the link decides which source it was.
    [Theory]
    [InlineData(null, "Counted from AniList's volume list. This series isn't linked to MangaUpdates yet. Checked 11 Oct.")]
    [InlineData(24348008396L, "Counted from MangaUpdates. The exact text it was read from is recorded from the next sync on. Checked 11 Oct.")]
    public void CountExplanation_NoRecordedBasis_ReadsFromTheLink(long? mangaUpdatesId, string expected) =>
        Series(basis: null, evidence: null, mangaUpdatesId).CountExplanation.ShouldHaveSingleItem().ShouldBe(expected);

    [Fact]
    public void CountExplanation_AniListChosen_SaysTheCountIsNotUsed()
    {
        var series = Series(VolumeCountBasis.EnglishPublisherNote, "Yen On: 16 Volumes; Ongoing", mangaUpdatesId: 1) with
        {
            SourceChoices =
            [
                new SourceChoiceResponse { Field = SyncDiscrepancyField.VolumeCount, Source = MetadataSourceName.AniList },
            ],
        };

        series.CountExplanation.ShouldHaveSingleItem().ShouldBe(
            "You chose AniList's volume list, so every volume tracked here counts as out. "
            + "MangaUpdates says “Yen On: 16 Volumes; Ongoing”, which is not used. Checked 11 Oct.");
    }

    [Theory]
    [InlineData(new[] { 7.5 }, "Vol 7.5 found in the Apple Books catalogue.")]
    [InlineData(new[] { 7.5, 11.5, 12.5 }, "Vols 7.5, 11.5 and 12.5 found in the Apple Books catalogue.")]
    public void CountExplanation_CatalogueBonusVolumes_AreNamed(double[] bonusNumbers, string expected)
    {
        var series = Series(VolumeCountBasis.EnglishPublisherNote, "Yen On: 9 Volumes + 1 Extra - Complete", mangaUpdatesId: 1) with
        {
            Volumes =
            [
                Volume(1),
                .. bonusNumbers.Select(number => Volume((decimal)number)),

                // Added by hand: not something the catalogue found.
                Volume(3.5m, isLocalOnly: true),
            ],
        };

        series.CountExplanation.Count.ShouldBe(2);
        series.CountExplanation[1].ShouldBe(expected);
    }

    [Theory]
    [InlineData(MetadataSourceName.OpenLibrary, true, "Date from Open Library")]
    [InlineData(MetadataSourceName.AppleBooks, true, "Date from Apple Books")]
    [InlineData(MetadataSourceName.OpenLibrary, false, null)]
    public void DateSourceLine_ADatedVolume_NamesItsSource(MetadataSourceName source, bool dated, string? expected) =>
        (Volume(4) with { ReleaseDate = dated ? new DateOnly(2026, 12, 9) : null, ReleaseDateSource = source })
            .DateSourceLine.ShouldBe(expected);

    [Fact]
    public void DateSourceLine_NoRecordedSource_IsNull() =>
        (Volume(4) with { ReleaseDate = new DateOnly(2026, 12, 9) }).DateSourceLine.ShouldBeNull();

    private static SeriesDetailResponse Series(VolumeCountBasis? basis, string? evidence, long? mangaUpdatesId) => new()
    {
        SeriesId = Guid.CreateVersion7(),
        TitleEnglish = "Overlord",
        Format = Format.LightNovel,
        PublicationStatus = PublicationStatus.Ongoing,
        TrackingStatus = TrackingStatus.Reading,
        Genres = [],
        HasNewVolume = false,
        MangaUpdatesId = mangaUpdatesId,
        Counts = new SeriesCountsResponse
        {
            Read = 0,
            Owned = 0,
            Published = 1,
            ToBuy = 1,
            Backlog = 0,
            Tracked = 1,
            NotOut = 0,
            Dated = 0,
        },
        CountProvenance = new CountProvenanceResponse { Basis = basis, Evidence = evidence, CheckedAt = Checked },
        Volumes = [Volume(1)],
    };

    private static VolumeResponse Volume(decimal number, bool isLocalOnly = false) => new()
    {
        VolumeId = Guid.CreateVersion7(),
        Number = number,
        IsOut = true,
        ReadStatus = ReadStatus.Unread,
        Owned = false,
        IsLocalOnly = isLocalOnly,
    };
}
