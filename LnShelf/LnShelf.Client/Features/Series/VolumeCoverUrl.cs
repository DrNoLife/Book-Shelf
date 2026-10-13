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

    /// <summary>
    /// What a CoverImage tries for this volume, best first: the cover <see cref="For"/> names,
    /// then the art behind an override (VolumeResponse.CoverFallbackObjectKey), so an override
    /// whose file is gone shows the synced cover instead of a broken image.
    /// </summary>
    public static IReadOnlyList<string> Candidates(ApiUrlBuilder apiUrl, VolumeResponse volume, int coverVersion)
    {
        ArgumentNullException.ThrowIfNull(apiUrl);
        ArgumentNullException.ThrowIfNull(volume);

        var candidates = new List<string>(2);
        if (For(apiUrl, volume, coverVersion) is { } url)
        {
            candidates.Add(url);
        }

        candidates.AddRange(apiUrl.Covers(volume.CoverFallbackObjectKey));
        return candidates;
    }
}
