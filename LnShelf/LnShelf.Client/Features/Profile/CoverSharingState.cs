namespace LnShelf.Client.Features.Profile;

/// <summary>
/// The signed-in user's "share my covers" setting, for <c>CoverSharingCard</c>.
/// </summary>
// Not optimistic and not part of PreferencesState, like ShelfSharingState: turning it on
// publishes covers on the server, so the card waits for the answer and shows the count the
// server reports rather than guessing. Consent is recorded server-side with its time.
public sealed class CoverSharingState(HttpClient httpClient)
{
    private const string Route = "api/community-covers/sharing";

    /// <summary>Null until the first load answers.</summary>
    public CoverSharingResponse? Sharing { get; private set; }

    public bool IsBusy { get; private set; }

    /// <summary>What went wrong last, or null.</summary>
    public string? Message { get; private set; }

    public event Action? Changed;

    public Task LoadAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            async () => await httpClient.GetFromJsonAsync<CoverSharingResponse>(Route, cancellationToken),
            "Could not load the cover sharing setting");

    public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        if (Sharing?.Enabled == enabled)
        {
            return Task.CompletedTask;
        }

        return RunAsync(
            async () =>
            {
                using var response = await httpClient.PutAsJsonAsync(
                    Route, new SetCoverSharingRequest { Enabled = enabled }, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var problem = await response.ReadProblemAsync(cancellationToken);
                    throw new HttpRequestException(problem ?? $"{(int)response.StatusCode} {response.StatusCode}.");
                }

                return await response.Content.ReadFromJsonAsync<CoverSharingResponse>(cancellationToken);
            },
            "Could not change cover sharing");
    }

    private async Task RunAsync(Func<Task<CoverSharingResponse?>> call, string refusal)
    {
        IsBusy = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            Sharing = await call() ?? Sharing;
        }
        catch (HttpRequestException exception)
        {
            Message = $"{refusal}: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }
}
