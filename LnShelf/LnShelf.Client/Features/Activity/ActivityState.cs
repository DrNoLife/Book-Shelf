using LnShelf.Shared.Activity;

namespace LnShelf.Client.Features.Activity;

/// <summary>
/// "Since your last visit": the one fetch, and "Got it".
/// </summary>
// A failed load shows no panel and no error: the panel is a courtesy on top of the shelf,
// and an error box where good news would have been is worse than silence. A failed "Got it"
// does say so, because the reader asked for something and it did not happen.
public sealed class ActivityState(HttpClient httpClient)
{
    /// <summary>Null while loading, after a failed load and once dismissed.</summary>
    public ActivityResponse? Activity { get; private set; }

    /// <summary>True while "Got it" is being saved.</summary>
    public bool IsDismissing { get; private set; }

    /// <summary>A failed "Got it", in a sentence. Cleared by the next attempt.</summary>
    public string? Message { get; private set; }

    /// <summary>Whether there is anything to show.</summary>
    public bool HasNews => Activity is { Series.Count: > 0 };

    public event Action? Changed;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Activity = await httpClient.GetFromJsonAsync<ActivityResponse>("api/activity", cancellationToken);
        }
        catch (HttpRequestException)
        {
            Activity = null;
        }

        Changed?.Invoke();
    }

    public async Task DismissAsync(CancellationToken cancellationToken = default)
    {
        IsDismissing = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            var request = new MarkActivitySeenRequest { Through = Activity?.Through };
            using var response = await httpClient.PostAsJsonAsync("api/activity/seen", request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                Activity = null;
            }
            else
            {
                Message = $"Could not save that: {(int)response.StatusCode} {response.StatusCode}.";
            }
        }
        catch (HttpRequestException)
        {
            Message = "Could not reach the API. The panel will be back next time.";
        }
        finally
        {
            IsDismissing = false;
        }

        Changed?.Invoke();
    }
}
