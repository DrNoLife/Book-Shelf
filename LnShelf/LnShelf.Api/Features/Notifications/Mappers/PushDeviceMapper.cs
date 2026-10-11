namespace LnShelf.Api.Features.Notifications.Mappers;

/// <summary>A stored subscription as the profile's device list shows it. Never the keys.</summary>
public static class PushDeviceMapper
{
    public static PushDeviceResponse ToResponse(PushSubscription subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new PushDeviceResponse
        {
            Id = subscription.Id,
            Endpoint = subscription.Endpoint,
            DeviceLabel = subscription.DeviceLabel,
            CreatedAt = subscription.CreatedAt,
            LastSuccessAt = subscription.LastSuccessAt,
            FailureCount = subscription.FailureCount,
        };
    }
}
