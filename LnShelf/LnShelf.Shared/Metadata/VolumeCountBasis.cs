namespace LnShelf.Shared.Metadata;

/// <summary>
/// Where a series' published-volume count was read from, so the series page can say why it
/// shows the number it does. Null on a series means unknown: counted before this was recorded.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<VolumeCountBasis>))]
public enum VolumeCountBasis
{
    /// <summary>MangaUpdates, from an English publisher's note ("Yen Press: 14 Volumes (Ongoing)").</summary>
    EnglishPublisherNote,

    /// <summary>MangaUpdates, from its status line, which counts the Japanese edition.</summary>
    MangaUpdatesStatus,

    /// <summary>AniList's volume count, used only when MangaUpdates gave none.</summary>
    AniList,

    /// <summary>Typed in by the user for a series no source knows.</summary>
    Manual,
}
