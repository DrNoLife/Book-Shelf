namespace LnShelf.Api.Features.Recommendations;

/// <summary>
/// The Recommendations slice registers itself; the composition root only calls this.
/// </summary>
public static class RecommendationsFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRecommendations()
        {
            services.AddScoped<IRecommendationDismissalQueryService, RecommendationDismissalQueryService>();
            services.AddScoped<IRecommendationDismissalCommandService, RecommendationDismissalCommandService>();
            return services;
        }
    }
}
