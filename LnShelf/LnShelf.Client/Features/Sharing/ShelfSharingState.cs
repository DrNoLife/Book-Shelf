using LnShelf.Shared.Sharing;

namespace LnShelf.Client.Features.Sharing;

/// <summary>
/// The signed-in user's "Who can see your shelf" setting, for <c>ShelfSharingCard</c>.
/// </summary>
// Not optimistic, unlike PreferencesState: the link only exists once the server has made it,
// so the card waits for the answer and shows what the server says rather than guessing.
public sealed class ShelfSharingState(HttpClient httpClient)
{
    private const string Route = "api/sharing";

    /// <summary>Null until the first load answers.</summary>
    public ShelfSharingResponse? Sharing { get; private set; }

    public bool IsBusy { get; private set; }

    /// <summary>What went wrong last, or null.</summary>
    public string? Message { get; private set; }

    public event Action? Changed;

    public Task LoadAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            async () => await httpClient.GetFromJsonAsync<ShelfSharingResponse>(Route, cancellationToken),
            "Could not load the sharing setting");

    public Task SetPublicAsync(bool isPublic, CancellationToken cancellationToken = default)
    {
        if (Sharing?.IsPublic == isPublic)
        {
            return Task.CompletedTask;
        }

        return RunAsync(
            async () =>
            {
                using var response = await httpClient.PutAsJsonAsync(
                    Route, new UpdateShelfSharingRequest { IsPublic = isPublic }, cancellationToken);
                return await ReadAsync(response, cancellationToken);
            },
            "Could not change who can see your shelf");
    }

    public Task RegenerateAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            async () =>
            {
                using var response = await httpClient.PostAsync($"{Route}/regenerate", content: null, cancellationToken);
                return await ReadAsync(response, cancellationToken);
            },
            "Could not make a new link");

    private async Task RunAsync(Func<Task<ShelfSharingResponse?>> call, string refusal)
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

    private static async Task<ShelfSharingResponse?> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.ReadProblemAsync(cancellationToken);
            throw new HttpRequestException(problem ?? $"{(int)response.StatusCode} {response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<ShelfSharingResponse>(cancellationToken);
    }
}
