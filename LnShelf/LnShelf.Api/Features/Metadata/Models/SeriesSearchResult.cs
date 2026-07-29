namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// One search hit, mapped out of the source's payload. A clean record: the dirty
/// AniList classes stop at the mapper (CLAUDE.md — Types).
/// </summary>
public sealed record SeriesSearchResult
{
    /// <summary>The source's own id — AniList's media id today.</summary>
    public required int ExternalId { get; init; }

    /// <summary>Never blank; romaji then native stand in when English is absent.</summary>
    public required string TitleEnglish { get; init; }

    public string? TitleRomaji { get; init; }
    public string? TitleNative { get; init; }

    public required Format Format { get; init; }
    public required PublicationStatus PublicationStatus { get; init; }

    /// <summary>Null when the source does not publish a total yet.</summary>
    public int? VolumeCount { get; init; }

    public int? StartYear { get; init; }
}
