namespace LnShelf.Client.Features.Calendar.Components;

public partial class CalendarLinkActions : IDisposable
{
    private CancellationTokenSource? _copiedReset;

    /// <summary>The feed's token; the links are built from it and the app's own address.</summary>
    [Parameter, EditorRequired]
    public required string Token { get; set; }

    /// <summary>Distinguishes the field's id where two of these could share a page.</summary>
    [Parameter]
    public string FieldId { get; set; } = "calendar-link";

    /// <summary>More actions on the same row, after Copy link.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required IJSRuntime JavaScript { get; set; }

    /// <summary>"Copied" on the button for two seconds after a copy.</summary>
    private bool HasCopied { get; set; }

    private string? CopyMessage { get; set; }

    private string HttpsLink => CalendarFeedLinks.Https(Navigation.BaseUri, Token);

    private string WebcalLink => CalendarFeedLinks.Webcal(Navigation.BaseUri, Token);

    public void Dispose()
    {
        _copiedReset?.Cancel();
        _copiedReset?.Dispose();
    }

    private async Task OnCopyAsync()
    {
        try
        {
            await JavaScript.InvokeVoidAsync("lnshelf.copyText", HttpsLink);
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
            // A second copy restarted the two seconds, or the component went away.
        }
    }
}
