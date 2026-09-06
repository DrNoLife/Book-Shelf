namespace LnShelf.Client.Features.Changelog.Pages;

public partial class ChangelogPage : IDisposable
{
    [Inject]
    public required ChangelogState State { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnStateChanged;
        await State.LoadAsync();
    }

    public void Dispose() => State.Changed -= OnStateChanged;

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
