using LnShelf.Client.Features.Series;

namespace LnShelf.Client.Features.Notifications.Components;

public partial class SeriesNotificationsToggle : IDisposable
{
    [Parameter]
    public required SeriesDetailResponse Series { get; set; }

    [Inject]
    public required SeriesState SeriesState { get; set; }

    [Inject]
    public required NotificationsState Notifications { get; set; }

    protected override async Task OnInitializedAsync()
    {
        Notifications.Changed += OnNotificationsChanged;

        // Once per visit to the app, not per series: the devices do not change between pages.
        if (Notifications.Settings is null)
        {
            await Notifications.LoadAsync();
        }
    }

    public void Dispose() => Notifications.Changed -= OnNotificationsChanged;

    private Task ToggleAsync() => SeriesState.SetNotificationsMutedAsync(!Series.NotificationsMuted);

    private void OnNotificationsChanged() => InvokeAsync(StateHasChanged);
}
