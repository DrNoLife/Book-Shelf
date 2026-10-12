namespace LnShelf.Api.Features.Shelf.Data;

public sealed class SeriesUserStateConfiguration : IEntityTypeConfiguration<SeriesUserState>
{
    public void Configure(EntityTypeBuilder<SeriesUserState> builder)
    {
        builder.ToTable("series_user_state");
        builder.HasKey(state => state.SeriesId);

        builder.Property(state => state.TrackingStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(state => state.VolumeViewOverride).HasConversion<string>().HasMaxLength(32);
        builder.Property(state => state.Notes).HasMaxLength(SeriesUserState.NotesMaximumLength);

        // A database default, not only a CLR one: the release before this one inserts user-state
        // rows without the column, and each of those must land as the Volumes series it is
        // (data-access.md, backward compatible for one release).
        builder.Property(state => state.ProgressUnit)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasDefaultValue(ProgressUnit.Volumes);

        // Unscaled numeric, like Volume.Number: a scaled column hands 142 back as 142.00, and
        // every place the number is printed would have to strip it again. That is also the
        // convention's mapping for a decimal, so nothing is configured.
    }
}
