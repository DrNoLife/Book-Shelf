using Lib.Net.Http.WebPush;
using LnShelf.Api.Commands;
using LnShelf.Api.Features.Notifications.Commands;

namespace LnShelf.Api.Features.Notifications;

/// <summary>
/// The Notifications slice registers itself; the composition root only calls this.
/// </summary>
public static class NotificationsFeatureExtensions
{
    /// <summary>A push service that has not answered by now is treated as down for this pass.</summary>
    private static readonly TimeSpan PushRequestTimeout = TimeSpan.FromSeconds(15);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddNotifications(IConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            services.AddOptions<WebPushOptions>()
                .Bind(configuration.GetSection(WebPushOptions.SectionName))
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<WebPushOptions>, WebPushOptionsValidator>();
            services.AddSingleton<WebPushAuthentication>();

            // Not the metadata sources' PoliteRateLimitHandler: push services are not donated
            // community APIs, and a sync pass sends one message per device.
            //
            // No redirects, and no automatic wait-and-retry on a 429: the endpoint was checked
            // against the known push services when it was saved (PushEndpointPolicy), and a
            // redirect would take the request somewhere that check never saw; a throttled
            // device is retried by the next pass instead of holding this one.
            services.AddHttpClient(nameof(PushServiceClient), httpClient => httpClient.Timeout = PushRequestTimeout)
                .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
                .AddTypedClient(httpClient => new PushServiceClient(httpClient) { AutoRetryAfter = false });

            services.AddScoped<IReleaseNotifier, WebPushReleaseNotifier>();
            services.AddScoped<IReleaseNotificationDispatcher, ReleaseNotificationDispatcher>();
            services.AddScoped<IPushSubscriptionQueryService, PushSubscriptionQueryService>();
            services.AddScoped<IPushSubscriptionCommandService, PushSubscriptionCommandService>();

            services.AddScoped<IConsoleCommand, VapidKeysCommand>();

            return services;
        }
    }
}
