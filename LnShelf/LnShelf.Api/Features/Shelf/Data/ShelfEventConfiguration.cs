namespace LnShelf.Api.Features.Shelf.Data;

public sealed class ShelfEventConfiguration : IEntityTypeConfiguration<ShelfEvent>
{
    public void Configure(EntityTypeBuilder<ShelfEvent> builder)
    {
        builder.ToTable("shelf_event");
        builder.HasKey(shelfEvent => shelfEvent.Id);

        // Assigned by the application (see SeriesConfiguration).
        builder.Property(shelfEvent => shelfEvent.Id).ValueGeneratedNever();

        builder.Property(shelfEvent => shelfEvent.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();

        // An event dies with its series. Cascade in the database too, so an older image that
        // deletes series with ExecuteDelete and knows nothing of this table still can.
        builder.HasOne(shelfEvent => shelfEvent.Series)
            .WithMany()
            .HasForeignKey(shelfEvent => shelfEvent.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        // "Since your last visit" reads a series' events newest first, after a point in time.
        builder.HasIndex(shelfEvent => new { shelfEvent.SeriesId, shelfEvent.OccurredAt });

        // The notification pass reads only what it has not dealt with, which is a handful of
        // rows in a table that keeps 120 days; the partial index holds just those.
        builder.HasIndex(shelfEvent => shelfEvent.NotifiedAt).HasFilter("\"NotifiedAt\" IS NULL");
    }
}
