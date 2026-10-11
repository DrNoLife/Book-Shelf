namespace LnShelf.Client.Features.Import.Pages;

public partial class ImportPage : IDisposable
{
    [Inject]
    public required ImportState ImportState { get; set; }

    private ImportQueueResponse? Queue => ImportState.Queue;

    private ImportCommitResponse? CommitResult => ImportState.CommitResult;

    /// <summary>Which of Upload · Check · Apply the open export is on.</summary>
    private int CurrentStep => Queue?.Batch switch
    {
        null => 1,
        { NeedsReview: > 0 } => 2,
        _ => 3,
    };

    protected override async Task OnInitializedAsync()
    {
        ImportState.Changed += OnImportStateChanged;
        await ImportState.LoadRecentBatchesAsync();
    }

    public void Dispose() => ImportState.Changed -= OnImportStateChanged;

    private void OnImportStateChanged() => InvokeAsync(StateHasChanged);

    private Task OnFileSubmittedAsync(IBrowserFile file) => ImportState.UploadAsync(file);

    private Task OnOpenBatchAsync(Guid batchId) => ImportState.OpenBatchAsync(batchId);

    private Task OnDecidedAsync(ImportDecision decision) =>
        ImportState.ResolveAsync(
            decision.GroupId, decision.AniListId, decision.Skip, decision.TreatReadAsOwned);

    private Task OnCommitAsync() => ImportState.CommitAsync();
}
