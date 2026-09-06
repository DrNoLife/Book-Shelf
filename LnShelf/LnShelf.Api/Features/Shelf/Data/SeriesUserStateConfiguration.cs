namespace LnShelf.Api.Features.Shelf.Data;

public sealed class SeriesUserStateConfiguration : IEntityTypeConfiguration<SeriesUserState>
{
    public void Configure(EntityTypeBuilder<SeriesUserState> builder)
    {
        builder.ToTable("series_user_state");
        builder.HasKey(state => state.SeriesId);

        builder.Property(state => state.TrackingStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(state => state.VolumeViewOverride).HasConversion<string>().HasMaxLength(32);
        builder.Property(state => state.Notes).HasMaxLength(4000);
    }
}
