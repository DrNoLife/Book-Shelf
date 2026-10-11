namespace LnShelf.Shared.Shelf;

/// <summary>
/// Adds a series no metadata source knows: a web novel, or anything the searches cannot
/// find (<c>POST api/series/manual</c>). It carries no external id, so the sync never
/// touches it; its volumes are the reader's own.
/// </summary>
public sealed record AddManualSeriesRequest
{
    /// <summary>The series' column width, so the form stops where the API would refuse.</summary>
    public const int MaximumTitleLength = 400;

    /// <summary>The author column's width.</summary>
    public const int MaximumAuthorLength = 200;

    /// <summary>The most volumes a series added by hand starts with, or can grow to.</summary>
    // A typo guard, not a policy: every volume is a row the reader then has to look after by
    // hand, and "2000" where "20" was meant would be a page of tiles to remove one by one.
    public const int MaximumVolumeCount = 200;

    public required string TitleEnglish { get; init; }

    public required Format Format { get; init; }

    public string? Author { get; init; }

    /// <summary>
    /// Volumes 1..N to create, all out. Null or zero adds the series with none; "Add volume"
    /// on the series page grows it later.
    /// </summary>
    public int? VolumeCount { get; init; }
}

/// <summary>
/// Adds one volume to a series added by hand (<c>POST api/series/{id}/volumes</c>).
/// </summary>
public sealed record AddLocalVolumeRequest
{
    /// <summary>
    /// The highest number a volume added by hand may carry.
    /// </summary>
    public const decimal MaximumNumber = 9999m;

    /// <summary>
    /// The volume's number. Null takes the next whole number after the highest one there; a
    /// decimal (7.5) adds a bonus volume between two main ones.
    /// </summary>
    public decimal? Number { get; init; }
}
