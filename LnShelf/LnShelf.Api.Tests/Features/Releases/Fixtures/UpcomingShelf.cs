namespace LnShelf.Api.Tests.Features.Releases.Fixtures;

/// <summary>
/// Builds series and volumes in the few shapes the Upcoming and Stats tests need.
/// </summary>
// Shared by both test classes because both are sums over the same entity graph, and a
// volume built one way here and another way there would be two opinions about what an
// "owned, unread, in print" volume looks like.
public static class UpcomingShelf
{
    public static Series CreateSeries(
        string title,
        Format format = Format.LightNovel,
        TrackingStatus? trackingStatus = TrackingStatus.Reading,
        params Volume[] volumes)
    {
        var seriesId = Guid.CreateVersion7();

        foreach (var volume in volumes)
        {
            volume.SeriesId = seriesId;
        }

        return new Series
        {
            Id = seriesId,
            TitleEnglish = title,
            Format = format,
            CoverObjectKey = $"series/{title}.webp",
            UserState = trackingStatus is { } status
                ? new SeriesUserState { SeriesId = seriesId, TrackingStatus = status }
                : null,
            Volumes = [.. volumes],
        };
    }

    /// <summary>A volume the release source says is in print.</summary>
    public static Volume InPrint(
        int number,
        bool owned = false,
        ReadStatus readStatus = ReadStatus.Unread,
        DateOnly? readAt = null,
        decimal? price = null,
        DateOnly? purchasedAt = null) =>
        CreateVolume(number, isReleased: true, releaseDate: null, owned, readStatus, readAt, price, purchasedAt);

    /// <summary>A volume not out yet — dated when a date is given, announced when not.</summary>
    public static Volume NotOut(int number, DateOnly? releaseDate = null, string? title = null)
    {
        var volume = CreateVolume(number, isReleased: false, releaseDate, owned: false, ReadStatus.Unread, null, null, null);
        volume.Title = title;
        return volume;
    }

    private static Volume CreateVolume(
        int number,
        bool isReleased,
        DateOnly? releaseDate,
        bool owned,
        ReadStatus readStatus,
        DateOnly? readAt,
        decimal? price,
        DateOnly? purchasedAt)
    {
        var volumeId = Guid.CreateVersion7();
        var touched = owned || readStatus is not ReadStatus.Unread;

        return new Volume
        {
            Id = volumeId,
            Number = number,
            IsReleased = isReleased,
            ReleaseDate = releaseDate,
            ExternalId = $"volume-{number}",
            UserState = touched
                ? new VolumeUserState
                {
                    VolumeId = volumeId,
                    Owned = owned,
                    ReadStatus = readStatus,
                    ReadAt = readAt,
                    PurchasePrice = price,
                    PurchasedAt = purchasedAt,
                }
                : null,
        };
    }
}
