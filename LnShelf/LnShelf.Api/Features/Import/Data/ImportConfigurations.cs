namespace LnShelf.Api.Features.Import.Data;

public sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("import_batch");
        builder.HasKey(batch => batch.Id);

        // Assigned by the application (see SeriesConfiguration).
        builder.Property(batch => batch.Id).ValueGeneratedNever();

        builder.Property(batch => batch.FileName).HasMaxLength(400).IsRequired();

        builder.HasMany(batch => batch.Groups)
            .WithOne()
            .HasForeignKey(group => group.ImportBatchId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ImportGroupConfiguration : IEntityTypeConfiguration<ImportGroup>
{
    public void Configure(EntityTypeBuilder<ImportGroup> builder)
    {
        builder.ToTable("import_group");
        builder.HasKey(group => group.Id);
        builder.Property(group => group.Id).ValueGeneratedNever();

        // Enums as text, for the same reason as the shelf's: a hobby database gets
        // read in psql, and text survives reordering the members.
        builder.Property(group => group.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(group => group.Reason).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.Property(group => group.SeriesKey).HasMaxLength(400).IsRequired();
        builder.Property(group => group.SeriesTitle).HasMaxLength(400).IsRequired();

        // One group per series key per batch — the grouping key is the identity.
        builder.HasIndex(group => new { group.ImportBatchId, group.SeriesKey }).IsUnique();

        builder.HasMany(group => group.Rows)
            .WithOne()
            .HasForeignKey(row => row.ImportGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(group => group.Candidates)
            .WithOne()
            .HasForeignKey(candidate => candidate.ImportGroupId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ImportRowConfiguration : IEntityTypeConfiguration<ImportRow>
{
    public void Configure(EntityTypeBuilder<ImportRow> builder)
    {
        builder.ToTable("import_row");
        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).ValueGeneratedNever();

        builder.Property(row => row.RawTitle).HasMaxLength(600).IsRequired();
        builder.Property(row => row.Author).HasMaxLength(400);
        builder.Property(row => row.Isbn).HasMaxLength(32);

        // Capped where Volume.Title is capped: this is the value that ends up there.
        builder.Property(row => row.Subtitle).HasMaxLength(400);

        builder.Property(row => row.ReadStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(row => row.OwnedFormat).HasConversion<string>().HasMaxLength(32);
    }
}

public sealed class ImportCandidateConfiguration : IEntityTypeConfiguration<ImportCandidate>
{
    public void Configure(EntityTypeBuilder<ImportCandidate> builder)
    {
        builder.ToTable("import_candidate");
        builder.HasKey(candidate => candidate.Id);
        builder.Property(candidate => candidate.Id).ValueGeneratedNever();

        builder.Property(candidate => candidate.TitleEnglish).HasMaxLength(400).IsRequired();
        builder.Property(candidate => candidate.TitleRomaji).HasMaxLength(400);
        builder.Property(candidate => candidate.TitleNative).HasMaxLength(400);
        builder.Property(candidate => candidate.CoverObjectKey).HasMaxLength(300);

        builder.Property(candidate => candidate.Format).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(candidate => candidate.PublicationStatus).HasConversion<string>().HasMaxLength(32).IsRequired();

        // The queue always reads a group's candidates in rank order.
        builder.HasIndex(candidate => new { candidate.ImportGroupId, candidate.Rank });
    }
}
