namespace LnShelf.Api.Tests.Client;

/// <summary>The smallest valid API answers the client state tests script.</summary>
internal static class ClientResponses
{
    public static SeriesDetailResponse Detail(
        Guid seriesId,
        string title,
        IReadOnlyList<VolumeResponse>? volumes = null,
        ProgressUnit progressUnit = ProgressUnit.Volumes,
        decimal? lastChapterRead = null) =>
        new()
        {
            SeriesId = seriesId,
            TitleEnglish = title,
            Format = Format.LightNovel,
            PublicationStatus = PublicationStatus.Ongoing,
            TrackingStatus = TrackingStatus.Reading,
            Genres = [],
            HasNewVolume = false,
            ProgressUnit = progressUnit,
            LastChapterRead = lastChapterRead,
            Counts = new SeriesCountsResponse
            {
                Read = 0,
                Owned = 0,
                Published = volumes?.Count ?? 0,
                ToBuy = 0,
                Backlog = 0,
                Tracked = volumes?.Count ?? 0,
                NotOut = 0,
                Dated = 0,
            },
            Volumes = volumes ?? [],
        };

    public static VolumeResponse Volume(
        decimal number,
        ReadStatus readStatus = ReadStatus.Unread,
        decimal? lastChapter = null) =>
        new()
        {
            VolumeId = Guid.CreateVersion7(),
            Number = number,
            IsOut = true,
            ReadStatus = readStatus,
            Owned = false,
            IsLocalOnly = false,
            LastChapter = lastChapter,
        };
}
