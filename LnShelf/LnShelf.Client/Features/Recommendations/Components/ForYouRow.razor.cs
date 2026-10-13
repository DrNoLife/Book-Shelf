namespace LnShelf.Client.Features.Recommendations.Components;

public partial class ForYouRow
{
    [Parameter]
    public required ForYouRowResponse Row { get; set; }

    [Inject]
    public required ForYouState State { get; set; }

    private string HeadingId => $"for-you-{Row.BecauseOfSeriesId}";
}
