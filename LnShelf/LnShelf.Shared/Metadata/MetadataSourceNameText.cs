namespace LnShelf.Shared.Metadata;

/// <summary>How a source is named to a person. The server and the client both print it.</summary>
public static class MetadataSourceNameText
{
    extension(MetadataSourceName source)
    {
        public string DisplayName => source switch
        {
            MetadataSourceName.AniList => "AniList",
            MetadataSourceName.MangaUpdates => "MangaUpdates",
            MetadataSourceName.OpenLibrary => "Open Library",
            MetadataSourceName.AppleBooks => "Apple Books",
            MetadataSourceName.MangaDex => "MangaDex",
            _ => source.ToString(),
        };
    }
}
