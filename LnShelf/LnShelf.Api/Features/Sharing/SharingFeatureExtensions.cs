using System.Threading.RateLimiting;
using LnShelf.Api.Features.Sharing.Abstractions;
using LnShelf.Api.Features.Sharing.Services;

namespace LnShelf.Api.Features.Sharing;

/// <summary>
/// The Sharing slice registers itself; the composition root only calls this.
/// </summary>
public static class SharingFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSharing(IConfiguration configuration)
        {
            services.AddOptions<SharingOptions>()
                .Bind(configuration.GetSection(SharingOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddScoped<IShelfShareResolver, ShelfShareResolver>();
            services.AddScoped<ISharedShelfQueryService, SharedShelfQueryService>();
            services.AddScoped<IShelfShareQueryService, ShelfShareQueryService>();
            services.AddScoped<IShelfShareCommandService, ShelfShareCommandService>();

            var options = configuration.GetSection(SharingOptions.SectionName).Get<SharingOptions>() ?? new SharingOptions();

            // AddRateLimiter composes through Configure, so these policies join the Auth slice's
            // and its 429 ProblemDetails (UserRateLimits.OnRejected) answers for them too.
            services.AddRateLimiter(limiter =>
            {
                limiter.AddPolicy(SharingRateLimits.SharedShelf, context => RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.RequestsPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

                // A bucket rather than a window: a shelf's covers all arrive in one burst.
                limiter.AddPolicy(SharingRateLimits.SharedCovers, context => RateLimitPartition.GetTokenBucketLimiter(
                    PartitionKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = options.CoverRequestsPerMinute,
                        TokensPerPeriod = options.CoverRequestsPerMinute,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
            });

            return services;
        }
    }

    // The visitor's address. Nobody is signed in here, so there is no user to partition by,
    // and partitioning by token would let one popular link throttle all its viewers. Right
    // behind the tunnel only because the forwarded headers walk two hops (AuthFeatureExtensions).
    private static string PartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
