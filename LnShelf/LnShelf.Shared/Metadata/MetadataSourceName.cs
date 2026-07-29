namespace LnShelf.Shared.Metadata;

/// <summary>
/// Which source a disagreement was resolved in favour of. Null means it was not
/// resolved at all and is waiting for a person.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<MetadataSourceName>))]
public enum MetadataSourceName
{
    AniList,
    MangaUpdates,
}
