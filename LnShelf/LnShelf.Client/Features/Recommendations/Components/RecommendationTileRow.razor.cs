namespace LnShelf.Client.Features.Recommendations.Components;

public partial class RecommendationTileRow : IDisposable
{
    [Parameter]
    public required IReadOnlyList<RecommendationResponse> Works { get; set; }

    /// <summary>The Not interested button renders only when something listens.</summary>
    [Parameter]
    public EventCallback<RecommendationResponse> NotInterested { get; set; }

    [Parameter]
    public bool IsDismissing { get; set; }

    [Inject]
    public required RecommendationAddState AddState { get; set; }

    protected override void OnInitialized() => AddState.Changed += OnAddStateChanged;

    public void Dispose() => AddState.Changed -= OnAddStateChanged;

    private void OnAddStateChanged() => InvokeAsync(StateHasChanged);
}
