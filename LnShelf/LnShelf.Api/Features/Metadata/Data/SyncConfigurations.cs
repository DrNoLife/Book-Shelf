namespace LnShelf.Api.Features.Metadata.Data;

public sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.ToTable("sync_run");
        builder.HasKey(run => run.Id);

        // Assigned by the application (see SeriesConfiguration).
        builder.Property(run => run.Id).ValueGeneratedNever();

        builder.Property(run => run.Summary).HasMaxLength(2000).IsRequired();

        // The sidebar asks for the owner's latest run on every navigation.
        builder.HasIndex(run => new { run.OwnerId, run.StartedAt });

        builder.HasOne<User>().WithMany().HasForeignKey(run => run.OwnerId).OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(run => run.Discrepancies)
            .WithOne()
            .HasForeignKey(discrepancy => discrepancy.SyncRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SyncDiscrepancyConfiguration : IEntityTypeConfiguration<SyncDiscrepancy>
{
    public void Configure(EntityTypeBuilder<SyncDiscrepancy> builder)
    {
        builder.ToTable("sync_discrepancy");
        builder.HasKey(discrepancy => discrepancy.Id);
        builder.Property(discrepancy => discrepancy.Id).ValueGeneratedNever();

        builder.Property(discrepancy => discrepancy.Field)
            .HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(discrepancy => discrepancy.ResolvedWith)
            .HasConversion<string>().HasMaxLength(32);

        builder.Property(discrepancy => discrepancy.ResolvedByChoice).HasDefaultValue(false);

        builder.Property(discrepancy => discrepancy.SeriesTitle).HasMaxLength(400).IsRequired();
        builder.Property(discrepancy => discrepancy.Note).HasMaxLength(1000);

        // Deliberately no foreign key to Series: a discrepancy outlives the series
        // it was recorded against, and cascading it away with a deleted series would
        // erase the record of why the shelf looked the way it did.
        //
        // The whole key a disagreement is looked up by, not the series alone: the history is
        // kept, and a standing disagreement is recorded again on every run, so a series'
        // rows grow by the night. SyncRunQueries.NewestDisagreementAsync asks for one
        // (series, field, volume) on every series-page load, and on SeriesId alone that was
        // a scan of everything the series ever disagreed about. SeriesId still leads, so a
        // query by series alone uses this index as it used the old one.
        builder.HasIndex(discrepancy => new { discrepancy.SeriesId, discrepancy.Field, discrepancy.VolumeNumber });

        builder.HasMany(discrepancy => discrepancy.Values)
            .WithOne()
            .HasForeignKey(value => value.SyncDiscrepancyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class SyncDiscrepancyValueConfiguration : IEntityTypeConfiguration<SyncDiscrepancyValue>
{
    public void Configure(EntityTypeBuilder<SyncDiscrepancyValue> builder)
    {
        builder.ToTable("sync_discrepancy_value");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Id).ValueGeneratedNever();

        builder.Property(value => value.Source)
            .HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(value => value.Value).HasMaxLength(400);

        // Always read with its discrepancy, never on its own.
        builder.HasIndex(value => value.SyncDiscrepancyId);
    }
}
