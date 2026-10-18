namespace LnShelf.Client.Features.Profile;

/// <summary>
/// The profile slice registers itself; the composition root only calls this.
/// </summary>
public static class ProfileClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddProfile()
        {
            // Page-local, like StatsState: only the profile page reads it.
            services.AddScoped<ProfileState>();

            // The owner's cover-sharing switch, for the card on the same page.
            services.AddScoped<CoverSharingState>();

            return services;
        }
    }
}
