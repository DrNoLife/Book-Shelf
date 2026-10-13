namespace LnShelf.Client.Features.Series.Components;

public partial class SourceConflictRow
{
    [Parameter]
    public required SourceConflictResponse Conflict { get; set; }

    /// <summary>True while a write is in flight: every button goes inert, like the rest of the page.</summary>
    [Parameter]
    public bool IsBusy { get; set; }

    /// <summary>The reader took this source's word.</summary>
    [Parameter]
    public EventCallback<MetadataSourceName> Chosen { get; set; }

    /// <summary>The reader handed the field back to the usual rules.</summary>
    [Parameter]
    public EventCallback ResetRequested { get; set; }

    /// <summary>
    /// Whether this is the source the reader picked — the one button that reads "Using".
    /// </summary>
    // Only a pick counts. A source the usual rules show is still offered as "Use X": choosing
    // it is how a reader settles a disagreement without changing what the shelf shows.
    private bool IsChosen(SourceOptionResponse option) =>
        option.IsDisplayed && Conflict.Reason is SourceDisplayReason.YourChoice;

    /// <summary>
    /// "Shown now" for the source the shelf is using, in words rather than colour.
    /// </summary>
    private static string StateWord(SourceOptionResponse option) =>
        option.IsDisplayed ? "Shown now" : "Not used";
}
