namespace LnShelf.Api.Features.Shelf.Data;

public sealed class VolumeConfiguration : IEntityTypeConfiguration<Volume>
{
    public void Configure(EntityTypeBuilder<Volume> builder)
    {
        builder.ToTable("volume");
        builder.HasKey(volume => volume.Id);

        builder.Property(volume => volume.ExternalId).HasMaxLength(100);
        builder.Property(volume => volume.CoverObjectKey).HasMaxLength(300);

        // Sync upserts key on (series, volume number), so re-running is safe.
        builder.HasIndex(volume => new { volume.SeriesId, volume.Number }).IsUnique();

        builder.HasOne(volume => volume.UserState)
            .WithOne()
            .HasForeignKey<VolumeUserState>(state => state.VolumeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
