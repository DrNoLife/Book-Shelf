using LnShelf.Shared.Notifications;

namespace LnShelf.Client.Features.Notifications;

/// <summary>
/// The signed-in user's notification devices and this browser's part in them, for the profile's
/// Notifications card and the series page's mute.
/// </summary>
// Not optimistic, like ShelfSharingState: a device is only on once the browser has subscribed
// and the server has the subscription, so the card shows what both of them say.
public sealed class NotificationsState(HttpClient httpClient, IJSRuntime javaScript)
{
    private const string Route = "api/notifications";

    /// <summary>Null until the first load answers.</summary>
    public NotificationsResponse? Settings { get; private set; }

    /// <summary>What this browser can do. Null until loaded, and when the server cannot push.</summary>
    public PushBrowserStatus? Browser { get; private set; }

    public bool IsBusy { get; private set; }

    /// <summary>What went wrong last, or null.</summary>
    public string? Message { get; private set; }

    /// <summary>What the last test reached, or null.</summary>
    public string? TestOutcome { get; private set; }

    public event Action? Changed;

    /// <summary>The server's row for this browser, when notifications are on here.</summary>
    public PushDeviceResponse? ThisDevice =>
        Browser is { Endpoint: { } endpoint, KeyMatches: true }
            ? Settings?.Devices.FirstOrDefault(device => device.Endpoint == endpoint)
            : null;

    /// <summary>Whether the series page offers a mute: the server can push and some device is on.</summary>
    public bool HasDevices => Settings is { Available: true, Devices.Count: > 0 };

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Message = null;

        try
        {
            await ReloadAsync(cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            Message = $"Could not load notifications: {exception.Message}";
        }
        catch (JSException)
        {
            // The browser threw while being asked what it supports: treat it as supporting nothing.
            Browser = new PushBrowserStatus();
        }
        finally
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Asks the browser for permission, subscribes it, and saves the subscription.</summary>
    // The interop call is the first statement, with nothing awaited before it: Safari only shows
    // the permission prompt inside the tap that asked, and an await first would spend that tap.
    // The public key was fetched when the card loaded, for the same reason.
    public async Task EnableAsync(CancellationToken cancellationToken = default)
    {
        var subscribing = javaScript.InvokeAsync<BrowserPushSubscription>(
            "lnshelf.subscribePush", cancellationToken, Settings?.PublicKey ?? string.Empty);

        IsBusy = true;
        Message = null;
        TestOutcome = null;
        Changed?.Invoke();

        try
        {
            var subscription = await subscribing;

            if (subscription is not { Endpoint: { } endpoint, P256dh: { } p256dh, Auth: { } auth })
            {
                Message = NotificationsText.EnableRefused(subscription?.Error);
                return;
            }

            using var response = await httpClient.PostAsJsonAsync(
                $"{Route}/subscriptions",
                new CreatePushSubscriptionRequest { Endpoint = endpoint, P256dh = p256dh, Auth = auth },
                cancellationToken);

            await EnsureSuccessAsync(response, "Could not turn notifications on", cancellationToken);
            await ReloadAsync(cancellationToken);
        }
        catch (JSException)
        {
            Message = NotificationsText.EnableRefused(error: null);
        }
        catch (HttpRequestException exception)
        {
            Message = exception.Message;
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }

    /// <summary>Turns notifications off for one device; for this one, the browser forgets it too.</summary>
    public Task RemoveAsync(PushDeviceResponse device, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(device);

        return RunAsync(async () =>
        {
            if (device.Id == ThisDevice?.Id)
            {
                await javaScript.InvokeAsync<string?>("lnshelf.unsubscribePush", cancellationToken);
            }

            using var response = await httpClient.DeleteAsync($"{Route}/subscriptions/{device.Id}", cancellationToken);
            if (response.StatusCode is not System.Net.HttpStatusCode.NotFound)
            {
                await EnsureSuccessAsync(response, "Could not turn notifications off", cancellationToken);
            }

            await ReloadAsync(cancellationToken);
        });
    }

    public Task SendTestAsync(CancellationToken cancellationToken = default) =>
        RunAsync(async () =>
        {
            using var response = await httpClient.PostAsync($"{Route}/test", content: null, cancellationToken);
            await EnsureSuccessAsync(response, "Could not send a test", cancellationToken);

            var result = await response.Content.ReadFromJsonAsync<PushTestResponse>(cancellationToken);
            TestOutcome = result is null ? null : NotificationsText.TestResult(result);

            // A device the push service called gone was removed by the test, and its row with it.
            await ReloadAsync(cancellationToken);
        });

    /// <summary>
    /// Signing out: this browser stops receiving the account's notifications. Best effort, and
    /// never in the way of the sign-out itself.
    /// </summary>
    public async Task ForgetThisDeviceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = await javaScript.InvokeAsync<string?>("lnshelf.unsubscribePush", cancellationToken);
            if (endpoint is null)
            {
                return;
            }

            // Looked up rather than remembered: the card may never have been opened in this visit.
            var settings = await httpClient.GetFromJsonAsync<NotificationsResponse>(Route, cancellationToken);
            if (settings?.Devices.FirstOrDefault(device => device.Endpoint == endpoint) is { } device)
            {
                using var response = await httpClient.DeleteAsync($"{Route}/subscriptions/{device.Id}", cancellationToken);
            }
        }
        catch (Exception exception) when (exception is JSException or HttpRequestException)
        {
            // The browser no longer has the subscription, or will be told it is gone the next
            // time a push is sent to it (the server then deletes the row). Either way the
            // sign-out goes ahead.
        }
        finally
        {
            Settings = null;
            Browser = null;
        }
    }

    private async Task ReloadAsync(CancellationToken cancellationToken)
    {
        Settings = await httpClient.GetFromJsonAsync<NotificationsResponse>(Route, cancellationToken);

        if (Settings is { Available: true, PublicKey: { } publicKey })
        {
            Browser = await javaScript.InvokeAsync<PushBrowserStatus>("lnshelf.pushStatus", cancellationToken, publicKey);
        }
    }

    private async Task RunAsync(Func<Task> operation)
    {
        IsBusy = true;
        Message = null;
        TestOutcome = null;
        Changed?.Invoke();

        try
        {
            await operation();
        }
        catch (HttpRequestException exception)
        {
            Message = exception.Message;
        }
        catch (JSException)
        {
            Message = "This browser could not do that. Try again in a moment.";
        }
        finally
        {
            IsBusy = false;
            Changed?.Invoke();
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, string refusal, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.ReadProblemAsync(cancellationToken);
            throw new HttpRequestException(problem ?? $"{refusal}: {(int)response.StatusCode} {response.StatusCode}.");
        }
    }
}
