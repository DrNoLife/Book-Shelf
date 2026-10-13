namespace LnShelf.Client.Features.Recommendations;

/// <summary>The Recommendations slice registers itself; the composition root only calls this.</summary>
public static class RecommendationsClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRecommendations()
        {
            services.AddScoped<RecommendationsClient>();
            services.AddScoped<ForYouState>();
            services.AddScoped<RecommendationAddState>();

            return services;
        }
    }
}
