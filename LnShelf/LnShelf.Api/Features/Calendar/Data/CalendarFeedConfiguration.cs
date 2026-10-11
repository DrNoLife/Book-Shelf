namespace LnShelf.Api.Features.Calendar.Data;

/// <summary>Not filtered by owner: it is how a calendar app's anonymous fetch learns whom to read for.</summary>
public sealed class CalendarFeedConfiguration : IEntityTypeConfiguration<CalendarFeed>
{
    public void Configure(EntityTypeBuilder<CalendarFeed> builder)
    {
        builder.ToTable("calendar_feed");
        builder.HasKey(feed => feed.UserId);
        builder.Property(feed => feed.UserId).ValueGeneratedNever();

        // Deleting an account deletes its feed.
        builder.HasOne<User>().WithMany().HasForeignKey(feed => feed.UserId).OnDelete(DeleteBehavior.Cascade);

        // Stored as is for ShelfShareConfiguration's reason: the profile page shows the link
        // again on every visit. Unique because the token alone finds the user.
        builder.Property(feed => feed.Token).HasMaxLength(CalendarFeed.MaximumTokenLength).IsRequired();
        builder.HasIndex(feed => feed.Token).IsUnique();
    }
}
