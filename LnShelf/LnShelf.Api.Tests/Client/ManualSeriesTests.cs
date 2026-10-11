using LnShelf.Client.Features.Series;

namespace LnShelf.Api.Tests.Client;

/// <summary>
/// When the series page offers "Add volume" and "Remove volume": the same rule the API
/// enforces, so the page never offers a button that answers 409.
/// </summary>
public sealed class ManualSeriesTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData(85470, null, false)]
    [InlineData(null, 17992364412L, false)]
    public void IsKeptByHand_ASeriesWithNeitherId_IsKeptByHand(int? aniListId, long? mangaUpdatesId, bool expected) =>
        ManualSeries.IsKeptByHand(Series(aniListId, mangaUpdatesId)).ShouldBe(expected);

    [Theory]
    [InlineData(null, true, true)]
    [InlineData(null, false, false)]
    [InlineData(85470, true, false)]
    public void IsRemovable_OnlyALocalVolumeOfASeriesKeptByHand(int? aniListId, bool isLocalOnly, bool expected) =>
        ManualSeries.IsRemovable(Series(aniListId, mangaUpdatesId: null), Volume(isLocalOnly)).ShouldBe(expected);

    private static SeriesDetailResponse Series(int? aniListId, long? mangaUpdatesId) => new()
    {
        SeriesId = Guid.CreateVersion7(),
        TitleEnglish = "Mother of Learning",
        Format = Format.WebNovel,
        PublicationStatus = PublicationStatus.Ongoing,
        TrackingStatus = TrackingStatus.Reading,
        Genres = [],
        HasNewVolume = false,
        AniListId = aniListId,
        MangaUpdatesId = mangaUpdatesId,
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
    };

    private static VolumeResponse Volume(bool isLocalOnly) => new()
    {
        VolumeId = Guid.CreateVersion7(),
        Number = 1,
        IsOut = true,
        ReadStatus = ReadStatus.Unread,
        Owned = false,
        IsLocalOnly = isLocalOnly,
    };
}
