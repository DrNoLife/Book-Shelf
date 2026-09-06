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
        /// <summary>"Ongoing", "Completed", "Hiatus", "Cancelled".</summary>
        public string Words => status switch
        {
            PublicationStatus.Ongoing => "Ongoing",
            PublicationStatus.Completed => "Completed",
            PublicationStatus.Hiatus => "Hiatus",
            PublicationStatus.Cancelled => "Cancelled",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status), status, "Unknown publication status."),
        };
    }
}
