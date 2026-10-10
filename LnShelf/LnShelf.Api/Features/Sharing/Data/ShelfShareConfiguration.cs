namespace LnShelf.Api.Features.Sharing.Data;

/// <summary>Not filtered by owner: it is how an anonymous request learns whom to act for.</summary>
public sealed class ShelfShareConfiguration : IEntityTypeConfiguration<ShelfShare>
{
    public void Configure(EntityTypeBuilder<ShelfShare> builder)
    {
        builder.ToTable("shelf_share");
        builder.HasKey(share => share.UserId);
        builder.Property(share => share.UserId).ValueGeneratedNever();

        // Deleting an account deletes its link.
        builder.HasOne<User>().WithMany().HasForeignKey(share => share.UserId).OnDelete(DeleteBehavior.Cascade);

        // Stored as is rather than hashed: the user page shows the link again on every visit,
        // and anyone able to read this table can read the series table, which is all a token
        // opens. Unique because the token alone finds the shelf.
        builder.Property(share => share.Token).HasMaxLength(32).IsRequired();
        builder.HasIndex(share => share.Token).IsUnique();
    }
}
