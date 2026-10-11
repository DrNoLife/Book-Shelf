namespace LnShelf.Api.Features.Notifications.Services;

/// <inheritdoc />
public sealed class PushSubscriptionQueryService(
    LnShelfDbContext dbContext,
    WebPushAuthentication authentication) : IPushSubscriptionQueryService
{
    public async Task<NotificationsResponse> GetAsync(CancellationToken cancellationToken)
    {
        var devices = await dbContext.PushSubscriptions
            .AsNoTracking()
            .OrderBy(subscription => subscription.CreatedAt)
            .ToListAsync(cancellationToken);

        return new NotificationsResponse
        {
            Available = authentication.IsAvailable,
            PublicKey = authentication.PublicKey,
            Devices = [.. devices.Select(PushDeviceMapper.ToResponse)],
        };
    }
}
