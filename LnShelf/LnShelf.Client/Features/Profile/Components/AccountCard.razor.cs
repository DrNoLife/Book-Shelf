using System.Net;
using LnShelf.Shared.Auth;

namespace LnShelf.Client.Features.Profile.Components;

public partial class AccountCard : IDisposable
{
    private readonly CancellationTokenSource _disposal = new();
    private ElementReference _typedInput;
    private string _typed = string.Empty;
    private bool _confirming;
    private bool _focusInput;
    private bool _busy;
    private string? _message;

    [Parameter]
    public required CurrentUserResponse User { get; set; }

    [Inject]
    public required ProfileState State { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    private bool CanDelete => DeleteConfirmation.Matches(_typed) && !_busy;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // The confirmation opens where the typing happens.
        if (_focusInput)
        {
            _focusInput = false;
            await _typedInput.FocusAsync();
        }
    }

    private void BeginConfirm()
    {
        _confirming = true;
        _focusInput = true;
        _message = null;
    }

    private void CancelConfirm()
    {
        _confirming = false;
        _typed = string.Empty;
    }

    /// <summary>
    /// Ends the session, then leaves the app for a static page: landing back in the app would
    /// ask the API for the library, get a 401 and sign straight back in through the provider.
    /// </summary>
    private async Task SignOutAsync()
    {
        _busy = true;

        try
        {
            await State.SignOutAsync(_disposal.Token);
            Navigation.NavigateTo("signed-out.html", forceLoad: true);
        }
        catch (HttpRequestException)
        {
            _message = "Could not sign out. Try again in a moment.";
            _busy = false;
        }
    }

    private async Task DeleteAsync()
    {
        // The button is disabled until the word matches, but Enter in the field submits the
        // form regardless, so the rule is checked here too.
        if (!CanDelete)
        {
            return;
        }

        _busy = true;
        _message = null;

        try
        {
            var status = await State.DeleteAccountAsync(_disposal.Token);

            if (status is HttpStatusCode.NoContent)
            {
                Navigation.NavigateTo("signed-out.html", forceLoad: true);
                return;
            }

            _message = $"Deleting failed ({(int)status}). Nothing was deleted; try again later.";
        }
        catch (HttpRequestException)
        {
            _message = "Could not reach the API. Nothing was deleted; try again later.";
        }

        _busy = false;
    }

    public void Dispose()
    {
        _disposal.Cancel();
        _disposal.Dispose();
    }
}
