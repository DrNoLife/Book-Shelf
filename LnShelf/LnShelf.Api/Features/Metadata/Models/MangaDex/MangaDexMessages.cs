namespace LnShelf.Api.Features.Metadata.Models.MangaDex;

// Dirty boundary models: classes, settable members, permissive nullability (CLAUDE.md —
// Types). Read by MangaDexSeriesMatcher and MangaDexAggregateMapper and never travelling
// further inward.

/// <summary>The answer to <c>GET /manga?title=…</c>, restricted to the fields the shelf reads.</summary>
public sealed class MangaDexMangaListResponse
{
    public string? Result { get; set; }

    public List<MangaDexManga>? Data { get; set; }
}

public sealed class MangaDexManga
{
    public Guid Id { get; set; }

    public MangaDexMangaAttributes? Attributes { get; set; }
}

public sealed class MangaDexMangaAttributes
{
    /// <summary>"ja" for manga; "ko" and "zh" for the manhwa and manhua whose "volumes" are seasons.</summary>
    public string? OriginalLanguage { get; set; }

    /// <summary>
    /// The record's links out, keyed by site: <c>al</c> (AniList id), <c>mu</c> (MangaUpdates slug).
    /// </summary>
    // A JsonElement rather than a dictionary: MangaDex's PHP heritage serializes an empty map as
    // an empty array (`"links": []`), and null is common too. MangaDexSeriesMatcher reads it
    // only when it really is an object.
    public JsonElement Links { get; set; }
}

/// <summary>
/// The answer to <c>GET /manga/{id}/aggregate</c>: chapters grouped by volume.
/// </summary>
// Volumes stays a JsonElement for the same heritage: an empty aggregate is `"volumes": []`
// rather than `{}`, and a volume's own `chapters` is sometimes an array instead of a keyed
// object. MangaDexAggregateMapper walks whichever shape arrives.
public sealed class MangaDexAggregateResponse
{
    public string? Result { get; set; }

    public JsonElement Volumes { get; set; }
}
