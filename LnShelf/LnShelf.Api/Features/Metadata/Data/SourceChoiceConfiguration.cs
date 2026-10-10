namespace LnShelf.Api.Features.Metadata.Data;

public sealed class SourceChoiceConfiguration : IEntityTypeConfiguration<SourceChoice>
{
    public void Configure(EntityTypeBuilder<SourceChoice> builder)
    {
        builder.ToTable("source_choice");
        builder.HasKey(choice => choice.Id);

        // Assigned by the application (see SeriesConfiguration).
        builder.Property(choice => choice.Id).ValueGeneratedNever();

        builder.Property(choice => choice.Field).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(choice => choice.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(choice => choice.ReplacedValue).HasMaxLength(400);

        // One choice per series field, or per volume for a per-volume field. Nulls are not
        // distinct, so two series-level choices for one field cannot both have a null volume
        // number. Needs Postgres 15 or later; compose runs 17.
        builder.HasIndex(choice => new { choice.SeriesId, choice.Field, choice.VolumeNumber })
            .IsUnique()
            .AreNullsDistinct(false);

        // A choice dies with its series, like volume user state: it is the user's data about
        // that series and means nothing without it.
        builder.HasOne(choice => choice.Series)
            .WithMany(series => series.SourceChoices)
            .HasForeignKey(choice => choice.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
