namespace LnShelf.Api.Features.Shelf.Data;

public sealed class VolumeUserStateConfiguration : IEntityTypeConfiguration<VolumeUserState>
{
    public void Configure(EntityTypeBuilder<VolumeUserState> builder)
    {
        builder.ToTable("volume_user_state");
        builder.HasKey(state => state.VolumeId);

        builder.Property(state => state.ReadStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(state => state.OwnedFormat).HasConversion<string>().HasMaxLength(32);
        builder.Property(state => state.PurchasePrice).HasPrecision(10, 2);
        builder.Property(state => state.CoverOverrideObjectKey).HasMaxLength(300);
    }
}
