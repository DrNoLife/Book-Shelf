using System.Net;

namespace LnShelf.Client.Features.Recommendations;

/// <summary>The recommendations endpoints: the series strip, For you and the hidden list.</summary>
public sealed class RecommendationsClient(HttpClient httpClient)
{
    public Task<SeriesRecommendationsResponse?> GetForSeriesAsync(Guid seriesId, CancellationToken cancellationToken) =>
        httpClient.GetFromJsonAsync<SeriesRecommendationsResponse>($"api/series/{seriesId}/recommendations", cancellationToken);

    public Task<ForYouResponse?> GetForYouAsync(CancellationToken cancellationToken) =>
        httpClient.GetFromJsonAsync<ForYouResponse>("api/recommendations", cancellationToken);

    public async Task<IReadOnlyList<RecommendationDismissalResponse>> GetDismissalsAsync(CancellationToken cancellationToken) =>
        await httpClient.GetFromJsonAsync<List<RecommendationDismissalResponse>>("api/recommendations/dismissals", cancellationToken) ?? [];

    public async Task DismissAsync(RecommendationResponse work, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PutAsJsonAsync(
            $"api/recommendations/dismissals/{work.AniListId}",
            new DismissRecommendationRequest { Title = work.TitleEnglish, Format = work.Format },
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task RestoreAsync(int aniListId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.DeleteAsync($"api/recommendations/dismissals/{aniListId}", cancellationToken);

        // Already shown again (restored from another tab) is the state that was asked for.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }
}
