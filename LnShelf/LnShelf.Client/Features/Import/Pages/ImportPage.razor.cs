using LnShelf.Client.Features.Import.Components;
using LnShelf.Shared.Import;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace LnShelf.Client.Features.Import.Pages;

public partial class ImportPage : IDisposable
{
    [Inject]
    public required ImportState ImportState { get; set; }

    private ImportQueueResponse? Queue => ImportState.Queue;

    private ImportBatchResponse? Batch => ImportState.Queue?.Batch;

    private ImportCommitResponse? CommitResult => ImportState.CommitResult;

    /// <summary>
    /// The count is computed into the label, the way design.md computes the next
    /// needed volume into "Vol 12 on Saxo".
    /// </summary>
    private string CommitLabel
    {
        get
        {
            if (Batch is null)
            {
                return "Add to the shelf";
            }

            var ready = Batch.AutoMatched + Batch.Resolved;
            return ready == 1 ? "Add 1 series to the shelf" : $"Add {ready} series to the shelf";
        }
    }

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
