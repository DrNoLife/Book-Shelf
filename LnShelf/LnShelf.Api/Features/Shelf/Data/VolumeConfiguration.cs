namespace LnShelf.Api.Features.Shelf.Data;

public sealed class VolumeConfiguration : IEntityTypeConfiguration<Volume>
{
    public void Configure(EntityTypeBuilder<Volume> builder)
    {
        builder.ToTable("volume");
        builder.HasKey(volume => volume.Id);

        // Assigned by the application, never by the store. This is the one that bit:
        // sync and import both add volumes to a series that is already tracked, and
        // a generated-key convention marks such a child Modified rather than Added.
        builder.Property(volume => volume.Id).ValueGeneratedNever();

        builder.Property(volume => volume.ExternalId).HasMaxLength(100);
        builder.Property(volume => volume.CoverObjectKey).HasMaxLength(300);

        // No index: enrichment reads a series' volumes and looks up each one's ISBN
        // externally. Nothing queries the shelf *by* ISBN, so an index would be
        // maintenance for no reader (data-access.md — no denormalisation until
        // something is measurably slow).
        builder.Property(volume => volume.Isbn).HasMaxLength(Isbn.MaximumLength);

        // Generous, because it holds a publisher's own full_title: "That Time I Got
        // Reincarnated As a Slime, Vol. 14 (light Novel)" is a real one.
        builder.Property(volume => volume.Title).HasMaxLength(400);

        // Uncapped, the one string here without a length — exactly the call
        // SeriesConfiguration makes about the series synopsis. A blurb is prose, so its
        // length is the source's business; uncapped means Postgres `text`, which costs
        // nothing over varchar(n) and cannot truncate a description mid-sentence.
        builder.Property(volume => volume.Description);

        // Sync upserts key on (series, volume number), so re-running is safe.
        builder.HasIndex(volume => new { volume.SeriesId, volume.Number }).IsUnique();

        builder.HasOne(volume => volume.UserState)
            .WithOne()
            .HasForeignKey<VolumeUserState>(state => state.VolumeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
