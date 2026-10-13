namespace LnShelf.Client.Features.Recommendations.Components;

public partial class DismissedList
{
    [Parameter]
    public required IReadOnlyList<RecommendationDismissalResponse> Dismissals { get; set; }

    [Inject]
    public required ForYouState State { get; set; }
}
