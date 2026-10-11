using LnShelf.Shared.Shelf;

namespace LnShelf.Shared.Metadata;

/// <summary>
/// What the release source offers for a series the sync could not link on its own.
/// </summary>
public sealed record ReleaseLinkCandidatesResponse
{
    /// <summary>The words actually searched for, so the search field can show them.</summary>
    public required string Query { get; init; }

    public required IReadOnlyList<ReleaseLinkCandidateResponse> Candidates { get; init; }
}

/// <summary>One search hit a person can check and link.</summary>
public sealed record ReleaseLinkCandidateResponse
{
    /// <summary>A long: MangaUpdates ids are eleven digits.</summary>
    public required long MangaUpdatesId { get; init; }

    public required string Title { get; init; }

    /// <summary>MangaUpdates' own medium word — "Novel", "Manga".</summary>
    public string? SourceType { get; init; }

    public int? StartYear { get; init; }

    /// <summary>
    /// Another series on this shelf already linked to this record, which is why linking it
    /// here is refused. Null when the record is free.
    /// </summary>
    public Guid? LinkedSeriesId { get; init; }

    public string? LinkedSeriesTitle { get; init; }
}

/// <summary>Links a series to its release-source record by hand.</summary>
public sealed record LinkReleaseSourceRequest
{
    public required long MangaUpdatesId { get; init; }
}

/// <summary>The series after a hand-made link, and whether its volumes were refreshed straight away.</summary>
public sealed record ReleaseLinkResponse
{
    public required SeriesDetailResponse Series { get; init; }

    /// <summary>
    /// False when the link is saved but the refresh did not run — a sync was already going, or
    /// the source failed — so the volumes follow at the next sync.
    /// </summary>
    public required bool Refreshed { get; init; }
}
