namespace LnShelf.Api.Extensions;

/// <summary>
/// The one place "what day is it" is answered.
/// </summary>
// Cross-slice for the same reason StringExtensions is: the shelf's whole derived state —
// to buy, backlog, behind — is a comparison against today, and the expression was written
// out longhand in six services. One of them getting UTC instead of local would silently
// move a release date by a day for half of every day.
public static class TimeProviderExtensions
{
    extension(TimeProvider timeProvider)
    {
        /// <returns>The current date in the machine's local time zone.</returns>
        // Local, not UTC, on purpose: a release date and a read date are calendar days
        // where the user is, not instants. At 01:00 in Copenhagen, UTC still says
        // yesterday, and a volume released "today" would render as unreleased.
        public DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetLocalNow().Date);
    }
}
