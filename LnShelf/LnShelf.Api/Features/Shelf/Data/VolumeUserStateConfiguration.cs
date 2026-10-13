namespace LnShelf.Api.Features.Shelf.Data;

public sealed class VolumeUserStateConfiguration : IEntityTypeConfiguration<VolumeUserState>
{
    public void Configure(EntityTypeBuilder<VolumeUserState> builder)
    {
        builder.ToTable("volume_user_state");
        builder.HasKey(state => state.VolumeId);

        builder.Property(state => state.ReadStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(state => state.OwnedFormat).HasConversion<string>().HasMaxLength(32);
        builder.Property(state => state.PurchasePrice).HasPrecision(VolumeUserState.PurchasePricePrecision, VolumeUserState.PurchasePriceScale);

        // A hand-written default so the previous image, which does not know the column, can
        // still insert rows (data-access.md, expand then contract).
        builder.Property(state => state.NotifyWhenDated).HasDefaultValue(false);
        builder.Property(state => state.CoverOverrideObjectKey).HasMaxLength(300);
    }
}
