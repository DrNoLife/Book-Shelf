namespace LnShelf.Api.Features.Covers.Data;

public sealed class CommunityCoverHideConfiguration : IEntityTypeConfiguration<CommunityCoverHide>
{
    public void Configure(EntityTypeBuilder<CommunityCoverHide> builder)
    {
        builder.ToTable("community_cover_hide");
        builder.HasKey(hide => hide.Id);

        // Assigned by the application (see SeriesConfiguration).
        builder.Property(hide => hide.Id).ValueGeneratedNever();

        builder.Property(hide => hide.ContentHash).HasColumnType($"char({CoverContentHash.Length})").IsRequired();

        // Per owner, like recommendation_dismissal: hiding twice is one row.
        builder.HasIndex(hide => new { hide.OwnerId, hide.ContentHash }).IsUnique();

        // Deleting an account deletes its hidden covers.
        builder.HasOne<User>().WithMany().HasForeignKey(hide => hide.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
