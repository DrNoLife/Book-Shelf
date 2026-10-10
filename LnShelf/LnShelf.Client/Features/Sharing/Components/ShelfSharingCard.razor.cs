namespace LnShelf.Client.Features.Sharing.Components;

public partial class ShelfSharingCard : IDisposable
{
    private static readonly IReadOnlyList<SegmentedOption<bool>> VisibilityOptions =
    [
        new(false, "Private", "Only you"),
        new(true, "Public by link", "Anyone with the link"),
    ];

    private CancellationTokenSource? _copiedReset;

    [Inject]
    public required ShelfSharingState State { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    private bool IsConfirmingNewLink { get; set; }

    /// <summary>"Copied" on the button for two seconds after a copy.</summary>
    private bool HasCopied { get; set; }

    private string? CopyMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnStateChanged;
        await State.LoadAsync();
    }

    public void Dispose()
    {
        State.Changed -= OnStateChanged;
        _copiedReset?.Cancel();
        _copiedReset?.Dispose();
    }

    private static string HelpText(bool isPublic) => isPublic
        ? "Anyone with the link can see your shelf. They do not need an account."
        : "Only you. A shelf link does not work while the shelf is private.";

    // Built here rather than by the API, so the API never needs to know the address it is
    // published under: the page is on the client's own origin.
    private string LinkFor(string token) => $"{Navigation.BaseUri}shared/{token}";

    private async Task OnVisibilityChangedAsync(bool isPublic)
    {
        IsConfirmingNewLink = false;
        CopyMessage = null;
        await State.SetPublicAsync(isPublic);
    }

    private void OnNewLinkRequested()
    {
        IsConfirmingNewLink = true;
        CopyMessage = null;
    }

    private void OnNewLinkCancelled() => IsConfirmingNewLink = false;

    private async Task OnNewLinkConfirmedAsync()
    {
        IsConfirmingNewLink = false;
        await State.RegenerateAsync();
    }

    private async Task OnCopyAsync(string token)
    {
        try
        {
            await JavaScript.InvokeVoidAsync("lnshelf.copyText", LinkFor(token));
        }
        catch (JSException)
        {
            // No clipboard outside a secure context (plain http on the home network).
            CopyMessage = "Copying is not available here. Select the link above and copy it.";
            return;
        }

        CopyMessage = null;
        await ShowCopiedAsync();
    }

    private async Task ShowCopiedAsync()
    {
        if (_copiedReset is not null)
        {
            await _copiedReset.CancelAsync();
            _copiedReset.Dispose();
        }

        _copiedReset = new CancellationTokenSource();
        var cancellationToken = _copiedReset.Token;

        HasCopied = true;
        StateHasChanged();

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            HasCopied = false;
        }
        catch (OperationCanceledException)
        {
            // A second copy restarted the two seconds, or the card went away.
        }
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
