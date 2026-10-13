namespace LnShelf.Api.Features.Recommendations.Data;

public sealed class RecommendationDismissalConfiguration : IEntityTypeConfiguration<RecommendationDismissal>
{
    public void Configure(EntityTypeBuilder<RecommendationDismissal> builder)
    {
        builder.ToTable("recommendation_dismissal");
        builder.HasKey(dismissal => dismissal.Id);

        // Assigned by the application (see SeriesConfiguration).
        builder.Property(dismissal => dismissal.Id).ValueGeneratedNever();

        builder.Property(dismissal => dismissal.Title).HasMaxLength(RecommendationDismissal.TitleMaximumLength).IsRequired();
        builder.Property(dismissal => dismissal.Format).HasConversion<string>().HasMaxLength(32).IsRequired();

        // Per owner, like push_subscription.
        builder.HasIndex(dismissal => new { dismissal.OwnerId, dismissal.AniListId }).IsUnique();

        // Deleting an account deletes its dismissals.
        builder.HasOne<User>().WithMany().HasForeignKey(dismissal => dismissal.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}
