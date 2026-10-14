namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// What the last chapter-map check concluded about a series, over every chapter-map source.
/// Stored as text on the series; null there means never checked.
/// </summary>
public enum ChapterMapStatus
{
    /// <summary>At least one source gave a map the shelf could use; its volumes carry ranges.</summary>
    Mapped,

    /// <summary>A source knew the series, but its map failed a sanity check (ChapterMapBuilder).</summary>
    Rejected,

    /// <summary>No source could say which of its records is this series.</summary>
    Unmatched,
}
