namespace LnShelf.Api.Features.Shelf.Services;

/// <summary>
/// The status filter, applied to an already-projected card.
/// </summary>
// Filtering the card rather than the entity means the derived-state rules stay
// in exactly one place (LibraryCardProjection). A second set of SQL predicates
// for "behind" and "backlog" would be a second definition to keep in step.
public static class LibraryCardFilters
{
    extension(LibraryCardResponse card)
    {
        public bool Matches(LibraryFilter filter) => filter switch
        {
            LibraryFilter.All => true,
            LibraryFilter.Behind => card.Behind,
            LibraryFilter.Backlog => card.Backlog > 0,

            // "Up to date" is ¬behind ∧ tracking; every series on the shelf is
            // tracked, so ¬behind is the whole test.
            LibraryFilter.UpToDate => !card.Behind,

            LibraryFilter.Reading => card.TrackingStatus == TrackingStatus.Reading,
            LibraryFilter.Completed => card.TrackingStatus == TrackingStatus.Completed,
            LibraryFilter.PlanToStart => card.TrackingStatus == TrackingStatus.PlanToStart,
            LibraryFilter.Dropped => card.TrackingStatus == TrackingStatus.Dropped,

            _ => throw new ArgumentOutOfRangeException(
                nameof(filter), filter, "Unknown library filter."),
        };
    }
}
