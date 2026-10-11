namespace LnShelf.Api.Features.Notifications.Data;

public sealed class PushSubscriptionConfiguration : IEntityTypeConfiguration<PushSubscription>
{
    public void Configure(EntityTypeBuilder<PushSubscription> builder)
    {
        builder.ToTable("push_subscription");
        builder.HasKey(subscription => subscription.Id);

        // Assigned by the application (see SeriesConfiguration).
        builder.Property(subscription => subscription.Id).ValueGeneratedNever();

        builder.Property(subscription => subscription.Endpoint).HasMaxLength(PushSubscription.MaximumEndpointLength).IsRequired();

        // Base64url keys: 87 and 22 characters in practice. The caps only refuse nonsense.
        builder.Property(subscription => subscription.P256dh).HasMaxLength(200).IsRequired();
        builder.Property(subscription => subscription.Auth).HasMaxLength(100).IsRequired();
        builder.Property(subscription => subscription.DeviceLabel).HasMaxLength(PushSubscription.MaximumDeviceLabelLength);

        // Per owner, not global: two people signing in on one browser get the same endpoint,
        // and the owner filter hides the first one's row from the second, so a global unique
        // index would turn the second subscribe into a 500 instead of an upsert.
        builder.HasIndex(subscription => new { subscription.OwnerId, subscription.Endpoint }).IsUnique();

        // Deleting an account deletes its devices.
        builder.HasOne<User>().WithMany().HasForeignKey(subscription => subscription.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
