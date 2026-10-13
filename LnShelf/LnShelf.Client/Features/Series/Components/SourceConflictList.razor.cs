namespace LnShelf.Client.Features.Series.Components;

public partial class SourceConflictList
{
    /// <summary>The disagreements a reader can settle by picking a source (not the link question).</summary>
    [Parameter]
    public required IReadOnlyList<SourceConflictResponse> Conflicts { get; set; }

    /// <summary>True while a write is in flight: every button goes inert, like the rest of the page.</summary>
    [Parameter]
    public bool IsBusy { get; set; }

    /// <summary>The reader took this source's word for this disagreement.</summary>
    [Parameter]
    public EventCallback<(SourceConflictResponse Conflict, MetadataSourceName Source)> Chosen { get; set; }

    /// <summary>The reader handed this disagreement back to the usual rules.</summary>
    [Parameter]
    public EventCallback<SourceConflictResponse> ResetRequested { get; set; }

    // The ones waiting on the reader first; a stable sort keeps the server's order within each.
    private IReadOnlyList<SourceConflictResponse> Ordered { get; set; } = [];

    protected override void OnParametersSet() =>
        Ordered = [.. Conflicts.OrderByDescending(conflict => conflict.NeedsReview)];
}
