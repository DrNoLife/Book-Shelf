namespace LnShelf.Client.Features.Search.Components;

public partial class SearchWorkCard
{
    [Parameter]
    public required SearchWork Work { get; set; }

    /// <summary>Null is "All": the rows then say which format each series is.</summary>
    [Parameter]
    public Format? FormatFilter { get; set; }

    /// <summary>The result the preview is showing, so its row is marked.</summary>
    [Parameter]
    public SeriesSearchResultResponse? SelectedResult { get; set; }

    [Parameter]
    public required Func<SeriesSearchResultResponse, bool> IsOnShelf { get; set; }

    /// <summary>A search or an add is in flight; every add on the card waits for it.</summary>
    [Parameter]
    public bool IsBusy { get; set; }

    /// <summary>The result whose add is in flight, so only its row shows the wait.</summary>
    [Parameter]
    public SeriesSearchResultResponse? AddingResult { get; set; }

    /// <summary>The add in flight has run long enough for its row to say why.</summary>
    [Parameter]
    public bool IsAddTakingLong { get; set; }

    /// <summary>"Adding 2 of 5…", while this card's add-all runs.</summary>
    [Parameter]
    public string? BatchProgress { get; set; }

    [Parameter]
    public EventCallback<SearchWorkEntry> Selected { get; set; }

    [Parameter]
    public EventCallback<SearchWorkEntry> Added { get; set; }

    [Parameter]
    public EventCallback<IReadOnlyList<SearchWorkEntry>> AddAllRequested { get; set; }

    private bool ShowFormat => FormatFilter is null;

    private AddAllOffer? Offer => AddAllOffer.For(Work, FormatFilter, IsOnShelf);

    /// <summary>Up to three covers, main story first, the front one first.</summary>
    private IEnumerable<(SearchWorkEntry Entry, int Depth)> StackedEntries =>
        Work.Entries.Take(3).Select((entry, depth) => (entry, depth));

    private bool IsSelected(SearchWorkEntry entry) => ReferenceEquals(entry.Result, SelectedResult);

    private bool IsAdding(SearchWorkEntry entry) => ReferenceEquals(entry.Result, AddingResult);

    private Task OnAddAllAsync(AddAllOffer offer) => AddAllRequested.InvokeAsync(offer.Entries);
}
