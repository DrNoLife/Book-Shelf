namespace LnShelf.Api.Features.Auth.Data;

/// <summary>Not filtered by owner: it is what the owner filters are about.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("app_user");
        builder.HasKey(user => user.Id);

        // Assigned by the application (see SeriesConfiguration).
        builder.Property(user => user.Id).ValueGeneratedNever();

        builder.Property(user => user.Issuer).HasMaxLength(200).IsRequired();
        builder.Property(user => user.Subject).HasMaxLength(200).IsRequired();
        builder.Property(user => user.Email).HasMaxLength(320).IsRequired();

        // Sign-in looks every user up by these two, and one identity is one user.
        builder.HasIndex(user => new { user.Issuer, user.Subject }).IsUnique();
    }
}
