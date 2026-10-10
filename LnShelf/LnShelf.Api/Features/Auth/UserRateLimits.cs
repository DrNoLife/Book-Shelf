using System.Threading.RateLimiting;

namespace LnShelf.Api.Features.Auth;

/// <summary>
/// Per-user rate limits on the endpoints that reach the metadata sources (UserLimitOptions).
/// </summary>
// Per user, not global: the sources' own limiters (MetadataRateLimiting) already protect the
// sources, and these keep one user's burst from using up everyone's share of them.
public static class UserRateLimits
{
    public const string ExternalSources = "external-sources";
    public const string WholeShelfSync = "whole-shelf-sync";

    extension(IServiceCollection services)
    {
        public IServiceCollection AddUserLimits(IConfiguration configuration)
        {
            services.AddOptions<UserLimitOptions>()
                .Bind(configuration.GetSection(UserLimitOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();
            services.AddScoped<UserLimits>();

            var limits = configuration.GetSection(UserLimitOptions.SectionName).Get<UserLimitOptions>()
                         ?? new UserLimitOptions();

            services.AddRateLimiter(limiter =>
            {
                limiter.AddPolicy(ExternalSources, context => RateLimitPartition.GetTokenBucketLimiter(
                    PartitionKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = limits.ExternalRequestsPerMinute,
                        TokensPerPeriod = limits.ExternalRequestsPerMinute,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

                limiter.AddPolicy(WholeShelfSync, context => RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = limits.SyncRunsPerHour,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    }));

                limiter.OnRejected = async (context, cancellationToken) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(
                        new ProblemDetailsContext
                        {
                            HttpContext = context.HttpContext,
                            ProblemDetails =
                            {
                                Status = StatusCodes.Status429TooManyRequests,
                                Title = "Too many requests",
                                Detail = "That was a lot in a short time. Wait a minute and try again.",
                            },
                        });
                };
            });

            return services;
        }
    }

    // The signed-in user. These endpoints all require one, so the fallback is only reached by
    // a request that is about to be refused anyway.
    private static string PartitionKey(HttpContext context) =>
        context.User.FindFirst(LnShelfClaims.UserId)?.Value ?? "anonymous";
}
