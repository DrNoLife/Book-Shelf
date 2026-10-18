namespace LnShelf.Api.Features.Covers.Data;

/// <summary>Not filtered by owner: it is published on purpose and read by match key.</summary>
public sealed class CommunityCoverConfiguration : IEntityTypeConfiguration<CommunityCover>
{
    public void Configure(EntityTypeBuilder<CommunityCover> builder)
    {
        builder.ToTable("community_cover", table =>
            table.HasCheckConstraint(
                "ck_community_cover_external_id",
                "\"AniListId\" IS NOT NULL OR \"MangaUpdatesId\" IS NOT NULL"));
        builder.HasKey(cover => cover.Id);

        // Assigned by the application (see SeriesConfiguration).
        builder.Property(cover => cover.Id).ValueGeneratedNever();

        // Deleting the sharer, or the volume the cover was published from, withdraws it.
        // No navigation on the volume: nobody can Include across shelves.
        builder.HasOne<User>().WithMany().HasForeignKey(cover => cover.UploaderUserId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Volume>().WithMany().HasForeignKey(cover => cover.SourceVolumeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(cover => cover.SourceVolumeId).IsUnique();

        builder.Property(cover => cover.Format).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(cover => cover.ContentHash).HasColumnType($"char({CoverContentHash.Length})").IsRequired();
        builder.Property(cover => cover.ObjectKey).HasMaxLength(300).IsRequired();

        // One match index per external id: a reader matches on whichever id its series has.
        builder.HasIndex(cover => new { cover.AniListId, cover.Format, cover.Number })
            .HasFilter("\"AniListId\" IS NOT NULL");
        builder.HasIndex(cover => new { cover.MangaUpdatesId, cover.Format, cover.Number })
            .HasFilter("\"MangaUpdatesId\" IS NOT NULL");

        // "Used by" and the image route look rows up by hash; withdrawal and the export by sharer.
        builder.HasIndex(cover => cover.ContentHash);
        builder.HasIndex(cover => cover.UploaderUserId);
    }
}
