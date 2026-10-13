namespace LnShelf.Client.Features.Recommendations.Pages;

public partial class ForYouPage : IDisposable
{
    [Inject]
    public required ForYouState State { get; set; }

    [Inject]
    public required RecommendationAddState AddState { get; set; }

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnStateChanged;
        AddState.Changed += OnStateChanged;
        await State.LoadAsync();
    }

    public void Dispose()
    {
        State.Changed -= OnStateChanged;
        AddState.Changed -= OnStateChanged;
        AddState.ClearMessage();
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
}
