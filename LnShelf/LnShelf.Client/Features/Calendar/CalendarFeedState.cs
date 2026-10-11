using LnShelf.Shared.Calendar;

namespace LnShelf.Client.Features.Calendar;

/// <summary>
/// The signed-in user's release calendar setting, shared by the Profile card and Upcoming's
/// "Subscribe in calendar".
/// </summary>
// Not optimistic, for ShelfSharingState's reason: the link only exists once the server has
// made it. Scoped, so turning the feed on from Upcoming is what the Profile card shows next.
public sealed class CalendarFeedState(HttpClient httpClient)
{
    private const string Route = "api/calendar";

    /// <summary>Null until the first load answers.</summary>
    public CalendarFeedResponse? Feed { get; private set; }

    public bool IsBusy { get; private set; }

    /// <summary>What went wrong last, or null.</summary>
    public string? Message { get; private set; }

    public event Action? Changed;

    public Task LoadAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            async () => await httpClient.GetFromJsonAsync<CalendarFeedResponse>(Route, cancellationToken),
            "Could not load the calendar setting");

    public Task SetOnAsync(bool isOn, CancellationToken cancellationToken = default)
    {
        if (Feed?.IsOn == isOn)
        {
            return Task.CompletedTask;
        }

        return RunAsync(
            async () =>
            {
                using var response = await httpClient.PutAsJsonAsync(
                    Route, new UpdateCalendarFeedRequest { IsOn = isOn }, cancellationToken);
                return await ReadAsync(response, cancellationToken);
            },
            "Could not change the calendar setting");
    }

    public Task RegenerateAsync(CancellationToken cancellationToken = default) =>
        RunAsync(
            async () =>
            {
                using var response = await httpClient.PostAsync($"{Route}/regenerate", content: null, cancellationToken);
                return await ReadAsync(response, cancellationToken);
            },
            "Could not make a new calendar link");

    private async Task RunAsync(Func<Task<CalendarFeedResponse?>> call, string refusal)
    {
        IsBusy = true;
        Message = null;
        Changed?.Invoke();

        try
        {
            Feed = await call() ?? Feed;
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

    private static async Task<CalendarFeedResponse?> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.ReadProblemAsync(cancellationToken);
            throw new HttpRequestException(problem ?? $"{(int)response.StatusCode} {response.StatusCode}.");
        }

        return await response.Content.ReadFromJsonAsync<CalendarFeedResponse>(cancellationToken);
    }
}
