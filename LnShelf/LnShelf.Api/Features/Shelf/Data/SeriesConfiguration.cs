namespace LnShelf.Api.Features.Shelf.Data;

public sealed class SeriesConfiguration : IEntityTypeConfiguration<Series>
{
    public void Configure(EntityTypeBuilder<Series> builder)
    {
        builder.ToTable("series");
        builder.HasKey(series => series.Id);

        // The application assigns every id (Guid.CreateVersion7, for sortable keys),
        // so EF must not treat a pre-set key as evidence the row already exists.
        // Left on the convention, a volume added to an already-tracked series is
        // discovered as Modified and SaveChanges throws "attempted to update an
        // entity that does not exist" (see VolumeConfiguration for the same reason).
        builder.Property(series => series.Id).ValueGeneratedNever();

        // Enums are stored as text: a hobby database gets read in psql, and text
        // survives reordering the enum members.
        builder.Property(series => series.Format).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(series => series.PublicationStatus).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(series => series.TitleEnglish).HasMaxLength(400).IsRequired();
        builder.Property(series => series.TitleRomaji).HasMaxLength(400);
        builder.Property(series => series.TitleNative).HasMaxLength(400);
        builder.Property(series => series.Publisher).HasMaxLength(200);
        builder.Property(series => series.Author).HasMaxLength(200);
        builder.Property(series => series.Illustrator).HasMaxLength(200);
        builder.Property(series => series.CoverObjectKey).HasMaxLength(300);

        // The one string here with no cap, on purpose: every other one is an identity
        // field where a length is a real constraint, and a synopsis is prose whose length
        // is the source's business. Uncapped means Postgres `text`, which costs nothing
        // over varchar(n) and cannot truncate a description mid-sentence.
        builder.Property(series => series.Description);

        // Matching and sync key on the external ids constantly (data-access.md).
        builder.HasIndex(series => series.AniListId).IsUnique();
        builder.HasIndex(series => series.MangaUpdatesId).IsUnique();

        builder.HasMany(series => series.Volumes)
            .WithOne()
            .HasForeignKey(volume => volume.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(series => series.UserState)
            .WithOne()
            .HasForeignKey<SeriesUserState>(state => state.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
