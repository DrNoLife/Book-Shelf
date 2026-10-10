namespace LnShelf.Client.Features.Sharing;

/// <summary>
/// The sharing slice registers itself; the composition root only calls this.
/// </summary>
public static class SharingClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSharing()
        {
            // The owner's setting, for the card on the user page.
            services.AddScoped<ShelfSharingState>();

            // The visitor's read of someone else's shelf; it holds nothing between calls.
            services.AddScoped<SharedShelfClient>();

            return services;
        }
    }
}
