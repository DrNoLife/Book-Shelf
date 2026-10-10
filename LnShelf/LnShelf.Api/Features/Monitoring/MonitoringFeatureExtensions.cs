namespace LnShelf.Api.Features.Monitoring;

/// <summary>
/// The Monitoring slice registers itself; the composition root only calls this.
/// </summary>
public static class MonitoringFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMonitoring(IConfiguration configuration)
        {
            services.AddOptions<HeartbeatOptions>()
                .Bind(configuration.GetSection(HeartbeatOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            // A plain client with a short timeout: a heartbeat that hangs would hold the job
            // that sent it, and the monitor is on the LAN.
            services.AddHttpClient<IHeartbeat, HttpHeartbeat>((provider, httpClient) =>
            {
                httpClient.Timeout = provider.GetRequiredService<IOptions<HeartbeatOptions>>().Value.Timeout;
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(LnShelfUserAgent.Value);
            });

            return services;
        }
    }
}
