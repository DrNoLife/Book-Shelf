namespace LnShelf.Client.Features.Profile.Pages;

public partial class ProfilePage : IDisposable
{
    [Inject]
    public required ProfileState State { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnStateChanged;
        await State.LoadAsync();
    }

    public void Dispose() => State.Changed -= OnStateChanged;

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
