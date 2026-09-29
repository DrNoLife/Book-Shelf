namespace LnShelf.Shared.Releases;

/// <summary>
/// The Upcoming view: what is out that you do not own, and what is still to come
/// (feature-spec §4.5).
/// </summary>
// Grouped on the server rather than handed over as one flat list, for the reason the
// library card computes its own counts: "out" is decided on three grounds (a past date,
// the release source's count, ownership), and a client that re-grouped a flat list
// would be re-deriving that rule and drifting from it.
public sealed record UpcomingResponse
{
    /// <summary>One entry per series with released volumes you do not own.</summary>
    public required IReadOnlyList<UpcomingEntryResponse> OutNow { get; init; }

    /// <summary>One entry per dated volume due later in the current calendar month.</summary>
    public required IReadOnlyList<UpcomingEntryResponse> ThisMonth { get; init; }

    /// <summary>One entry per dated volume due in the three calendar months after this one.</summary>
    public required IReadOnlyList<UpcomingEntryResponse> NextThreeMonths { get; init; }

    /// <summary>One entry per dated volume due after that.</summary>
    // Not one of the spec's four groups. A volume dated five months out is neither
    // "next 3 months" nor "no date", and filing it under either would print something
    // untrue about it.
    public required IReadOnlyList<UpcomingEntryResponse> Later { get; init; }

    /// <summary>One entry per series with volumes known to exist but carrying no date.</summary>
    public required IReadOnlyList<UpcomingEntryResponse> Announced { get; init; }
}
