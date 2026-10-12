namespace LnShelf.Client.Features.Import.Components;

public partial class ImportReview
{
    [Parameter]
    public required ImportQueueResponse Queue { get; set; }

    /// <summary>Blocks every control while the queue is being written to.</summary>
    [Parameter]
    public bool IsBusy { get; set; }

    /// <summary>Apply is running: its button shows the wait.</summary>
    [Parameter]
    public bool IsCommitting { get; set; }

    [Parameter]
    public EventCallback<ImportDecision> Decided { get; set; }

    [Parameter]
    public EventCallback CommitRequested { get; set; }

    private ImportBatchResponse Batch => Queue.Batch;

    private bool IsApplied => Batch.CommittedAt is not null;

    private List<ImportGroupResponse> Waiting =>
        [.. Queue.Groups.Where(group => group.Status == ImportGroupStatus.NeedsReview)];

    private List<ImportGroupResponse> Settled =>
        [.. Queue.Groups.Where(group => group.Status != ImportGroupStatus.NeedsReview)];

    /// <summary>"19 series matched or decided" — what is behind the fold.</summary>
    private string SettledSummary => Settled.Count == 1
        ? "1 series matched or decided — check or change it"
        : $"{Settled.Count} series matched or decided — check or change them";

    /// <summary>"72 books · 19 series matched · 2 left off".</summary>
    private string CountsLine
    {
        get
        {
            var matched = IsApplied ? Batch.Committed : Batch.AutoMatched + Batch.Resolved;
            var parts = new List<string>
            {
                Batch.RowCount == 1 ? "1 book" : $"{Batch.RowCount} books",
                $"{matched} series {(IsApplied ? "on the shelf" : "matched")}",
            };

            if (Batch.Skipped > 0)
            {
                parts.Add($"{Batch.Skipped} left off");
            }

            return string.Join(" · ", parts);
        }
    }

    /// <summary>
    /// The count is computed into the label, the way design.md computes the next needed
    /// volume into "Vol 12 on Saxo" — and while it cannot run, it says what is in the way.
    /// </summary>
    private string ApplyLabel
    {
        get
        {
            if (Batch.NeedsReview > 0)
            {
                return $"Check {Batch.NeedsReview} more to apply";
            }

            var ready = Batch.AutoMatched + Batch.Resolved;
            return ready == 1 ? "Add 1 series to the shelf" : $"Add {ready} series to the shelf";
        }
    }
}
