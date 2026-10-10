namespace LnShelf.Client.Features.Series;

/// <summary>Where a volume's cover is drawn from: the local cover route, never a CDN.</summary>
public static class VolumeCoverUrl
{
    /// <summary>
    /// The override already arrives as the volume's cover key (the projection prefers it),
    /// so only the cache needs defeating when there is one: the key never changes, and
    /// <paramref name="coverVersion"/> is what tells the browser the bytes behind it did.
    /// </summary>
    public static string? For(ApiUrlBuilder apiUrl, VolumeResponse volume, int coverVersion)
    {
        ArgumentNullException.ThrowIfNull(apiUrl);
        ArgumentNullException.ThrowIfNull(volume);

        if (volume.CoverObjectKey is not { } coverObjectKey)
        {
            return null;
        }

        var url = apiUrl.Cover(coverObjectKey);
        return volume.HasCoverOverride ? $"{url}?v={coverVersion}" : url;
    }
}
