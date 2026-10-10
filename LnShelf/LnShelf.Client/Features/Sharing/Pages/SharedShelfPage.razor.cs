using LnShelf.Shared.Sharing;

namespace LnShelf.Client.Features.Sharing.Pages;

public partial class SharedShelfPage : IDisposable
{
    private CancellationTokenSource? _loading;

    [Inject]
    public required SharedShelfClient Client { get; set; }

    [Parameter]
    public required string Token { get; set; }

    private SharedShelfResponse? Shelf { get; set; }

    /// <summary>The link opens nothing: unknown, revoked or mistyped. One message for all three.</summary>
    private bool IsNotShared { get; set; }

    private string? Message { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        // A new token (a second link opened in the same tab) cancels the first load, so a
        // slow answer for the old link can never land on the new one.
        if (_loading is not null)
        {
            await _loading.CancelAsync();
            _loading.Dispose();
        }

        _loading = new CancellationTokenSource();
        var cancellationToken = _loading.Token;

        Shelf = null;
        IsNotShared = false;
        Message = null;

        try
        {
            Shelf = await Client.GetAsync(Token, cancellationToken);
            IsNotShared = Shelf is null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Superseded by the next token; that load renders its own answer.
        }
        catch (HttpRequestException exception)
        {
            Message = exception.Message;
        }
    }

    public void Dispose()
    {
        _loading?.Cancel();
        _loading?.Dispose();
    }

    private static string SeriesCountText(int count) => count == 1 ? "1 series" : $"{count} series";

    /// <summary>The cards under a heading per tracking status, in the order the API sends them.</summary>
    private static IEnumerable<CardGroup> Groups(SharedShelfResponse shelf) =>
        new (TrackingStatus Status, string Title, string HeadingId)[]
            {
                (TrackingStatus.Reading, "Reading now", "shared-reading"),
                (TrackingStatus.Completed, "Completed", "shared-completed"),
                (TrackingStatus.PlanToStart, "Plan to start", "shared-plan-to-start"),
            }
            .Select(group => new CardGroup(
                group.Title,
                group.HeadingId,
                [.. shelf.Series.Where(card => card.TrackingStatus == group.Status)]))
            .Where(group => group.Cards.Count > 0);

    private sealed record CardGroup(string Title, string HeadingId, IReadOnlyList<SharedSeriesCardResponse> Cards);
}
