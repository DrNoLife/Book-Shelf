using LnShelf.Api.Features.Activity.Abstractions;

namespace LnShelf.Api.Features.Activity;

/// <summary>
/// The Activity slice registers itself; the composition root only calls this.
/// </summary>
// "Since your last visit" only. The events it reads are written by the sync (Metadata) and
// pushed by Notifications; this slice owns the reader's view of them and "Got it".
public static class ActivityFeatureExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddActivity()
        {
            services.AddScoped<IActivityQueryService, ActivityQueryService>();
            services.AddScoped<IActivityCommandService, ActivityCommandService>();
            return services;
        }
    }
}
