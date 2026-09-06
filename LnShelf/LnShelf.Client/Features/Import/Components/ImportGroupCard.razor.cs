namespace LnShelf.Client.Features.Import.Components;

public partial class ImportGroupCard
{
    [Parameter]
    public required ImportGroupResponse Group { get; set; }

    /// <summary>Blocks every control while the queue is being written to.</summary>
    [Parameter]
    public bool IsBusy { get; set; }

    /// <summary>(groupId, aniListId, skip, treatReadAsOwned)</summary>
    [Parameter]
    public EventCallback<ImportDecision> Decided { get; set; }

    [Inject]
    public required ImportState ImportState { get; set; }

    private bool IsRowsOpen { get; set; }
    private bool IsSearchOpen { get; set; }
    private bool IsSearching { get; set; }
    private bool HasSearched { get; set; }
    private string SearchQuery { get; set; } = string.Empty;
    private IReadOnlyList<ImportCandidateResponse> SearchResults { get; set; } = [];

    private bool IsCommitted => Group.Status is ImportGroupStatus.Committed;

    private string StatusLabel => Group.Status switch
    {
        ImportGroupStatus.AutoMatched => "Matched",
        ImportGroupStatus.NeedsReview => "Needs you",
        ImportGroupStatus.Resolved => "Chosen",
        ImportGroupStatus.Skipped => "Left off",
        ImportGroupStatus.Committed => "On the shelf",
        _ => Group.Status.ToString(),
    };

    private string StatusClass => Group.Status switch
    {
        ImportGroupStatus.NeedsReview => "group__status--waiting",
        ImportGroupStatus.Skipped => "group__status--quiet",
        _ => "group__status--settled",
    };

    /// <summary>
    /// Says what is actually uncertain. The format case is the common one for a
    /// book export and the only one a person can resolve instantly, so it gets the
    /// plainest sentence.
    /// </summary>
    private string? ReasonLine => Group.Reason switch
    {
        ImportReviewReason.FormatAmbiguous =>
            "A light novel and a manga share this title — only you know which you read.",
        ImportReviewReason.AmbiguousCandidates =>
            "Two candidates scored the same on title alone.",
        ImportReviewReason.LowConfidence =>
            $"Closest title only scored {Group.MatchScore}.",
        ImportReviewReason.NoCandidate =>
            "AniList has nothing by this name.",
        _ => null,
    };

    private Task OnCandidateSelectedAsync(int aniListId) =>
        Decided.InvokeAsync(new ImportDecision(Group.GroupId, aniListId, Skip: false, Group.TreatReadAsOwned));

    private Task OnSkippedAsync() =>
        Decided.InvokeAsync(new ImportDecision(Group.GroupId, AniListId: null, Skip: true, Group.TreatReadAsOwned));

    private Task OnOwnershipToggledAsync() =>
        Decided.InvokeAsync(new ImportDecision(
            Group.GroupId,
            Group.MatchedAniListId,
            Skip: Group.Status is ImportGroupStatus.Skipped,
            !Group.TreatReadAsOwned));

    private string RowsLabel => IsRowsOpen
        ? "Hide the books"
        : Group.RowCount == 1 ? "Show the 1 book" : $"Show the {Group.RowCount} books";

    private void ToggleRows() => IsRowsOpen = !IsRowsOpen;

    /// <summary>The volume number as it was parsed, or a plain admission that it was not.</summary>
    // "no volume" rather than a guess or a dash: the whole point of the panel is that a
    // wrong or missing parse is visible before anything is written to the shelf.
    private static string VolumeLabel(ImportRowResponse row) =>
        row.VolumeNumber is { } number ? $"Vol {number}" : "no volume";

    private static string ReadLabel(ImportRowResponse row) => row.ReadStatus switch
    {
        ReadStatus.Read => "Read",
        ReadStatus.Reading => "Reading",
        _ => "Unread",
    };

    private void ToggleManualSearch()
    {
        IsSearchOpen = !IsSearchOpen;

        if (IsSearchOpen && SearchQuery.Length == 0)
        {
            // Seeded with the title that failed, because that is what the user is
            // about to retype.
            SearchQuery = Group.SeriesTitle;
        }
    }

    private void OnSearchInput(ChangeEventArgs args) =>
        SearchQuery = args.Value?.ToString() ?? string.Empty;

    private async Task RunSearchAsync()
    {
        IsSearching = true;
        SearchResults = await ImportState.SearchCandidatesAsync(SearchQuery);
        HasSearched = true;
        IsSearching = false;
    }
}
