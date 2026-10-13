namespace LnShelf.Client.Components;

/// <summary>
/// Publication status in words, in one place.
/// </summary>
// The same switch existed twice and the raw enum was printed three more times. They
// agreed by luck rather than by construction: an enum value renamed on the wire would
// have changed three surfaces silently and left the other two saying something else.
public static class PublicationStatusText
{
    extension(PublicationStatus status)
    {
        /// <summary>"Still publishing", "Finished publishing", "On hiatus", "Cancelled".</summary>
        // Never the bare "Completed": the tracking-status control next to it says Completed
        // for the reader's own status, and the two read as one (owner, 2026-10-13).
        public string Words => status switch
        {
            PublicationStatus.Ongoing => "Still publishing",
            PublicationStatus.Completed => "Finished publishing",
            PublicationStatus.Hiatus => "On hiatus",
            PublicationStatus.Cancelled => "Cancelled",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status), status, "Unknown publication status."),
        };
    }
}
