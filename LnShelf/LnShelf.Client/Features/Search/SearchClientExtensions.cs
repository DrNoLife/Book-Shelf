namespace LnShelf.Client.Features.Search;

/// <summary>
/// The search &amp; add slice registers itself; the composition root only calls this.
/// </summary>
public static class SearchClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSearch()
        {
            services.AddScoped<SeriesSearchState>();

            return services;
        }
    }
}
