namespace LnShelf.Api.Features.Sharing.Mappers;

/// <summary>
/// Orders the shared cards and adds up the totals, in memory.
/// </summary>
// In memory because the order is by a rank of the tracking status, which reads more plainly
// as a switch than as a translated CASE, and a shelf is a few hundred rows at most.
public static class SharedShelfMapper
{
    public static SharedShelfResponse ToResponse(IReadOnlyList<SharedSeriesCardResponse> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        return new SharedShelfResponse
        {
            Totals = new SharedShelfTotals
            {
                VolumesRead = cards.Sum(card => card.Read),
                SeriesReading = cards.Count(card => card.TrackingStatus is TrackingStatus.Reading),
                SeriesCompleted = cards.Count(card => card.TrackingStatus is TrackingStatus.Completed),
            },
            Series =
            [
                .. cards
                    .OrderBy(card => Rank(card.TrackingStatus))
                    .ThenBy(card => card.TitleEnglish, StringComparer.OrdinalIgnoreCase),
            ],
        };
    }

    // What the owner is reading now first, then what they finished, then what waits.
    private static int Rank(TrackingStatus status) => status switch
    {
        TrackingStatus.Reading => 0,
        TrackingStatus.Completed => 1,
        TrackingStatus.PlanToStart => 2,
        TrackingStatus.Dropped => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown tracking status."),
    };
}
