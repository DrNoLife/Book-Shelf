namespace LnShelf.Client.Components;

public partial class CoverImage
{
    private readonly CoverCandidates _candidates = new();

    /// <summary>Image URLs, best first. Nulls and blanks are skipped, so a missing key needs no guard.</summary>
    [Parameter]
    public IEnumerable<string?>? Sources { get; set; }

    /// <summary>What shows when there is no source, or every one failed to load. Nothing by default.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>The img's own attributes: class, loading, alt.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    protected override void OnParametersSet() => _candidates.Offer(Sources);

    // The event's own re-render is what swaps the img to the next source or the placeholder.
    private void OnImageFailed(string source) => _candidates.Fail(source);
}
