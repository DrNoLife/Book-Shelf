namespace LnShelf.Client.Features.Notifications;

/// <summary>
/// The notifications slice registers itself; the composition root only calls this.
/// </summary>
public static class NotificationsClientExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddNotifications()
        {
            // Scoped, so the profile card, the series page's mute and sign-out share one view
            // of this browser's subscription.
            services.AddScoped<NotificationsState>();

            return services;
        }
    }
}
