namespace LnShelf.Shared.Shelf;

/// <summary>
/// One round trip for the whole library view: the cards the current filter
/// shows, plus the totals the header prints and the shelf counts the sidebar
/// prints — which are counted across the format scope, not the filtered subset.
/// </summary>
public sealed record LibraryResponse
{
    public required LibraryTotals Totals { get; init; }
    public required LibraryShelfCounts Shelves { get; init; }
    public required IReadOnlyList<LibraryCardResponse> Cards { get; init; }
}

/// <summary>
/// Header numbers, in the user's terms: "43 bought and unread. 190 out there
/// you haven't bought."
/// </summary>
public sealed record LibraryTotals
{
    public required int SeriesCount { get; init; }
    public required int Read { get; init; }
    public required int Owned { get; init; }
    public required int Published { get; init; }

    /// <summary>Owned and unread, summed over every series in scope.</summary>
    public required int Backlog { get; init; }

    /// <summary>Released and unowned, summed over every series in scope.</summary>
    public required int ToBuy { get; init; }
}

/// <summary>
/// The sidebar's shelves. Series counts except <see cref="BacklogVolumes"/>,
/// which counts volumes — the sidebar labels it "43 vols" for exactly that reason.
/// </summary>
public sealed record LibraryShelfCounts
{
    public required int Reading { get; init; }
    public required int BehindOnBuying { get; init; }
    public required int BacklogVolumes { get; init; }
    public required int UpToDate { get; init; }
    public required int Completed { get; init; }
    public required int PlanToStart { get; init; }
    public required int Dropped { get; init; }
}
