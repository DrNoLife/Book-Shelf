namespace LnShelf.Api.Features.Preferences.Data;

public sealed class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("user_preferences");
        builder.HasKey(preferences => preferences.OwnerId);
        builder.Property(preferences => preferences.OwnerId).ValueGeneratedNever();

        // Deleting an account deletes its preferences.
        builder.HasOne<User>().WithMany().HasForeignKey(preferences => preferences.OwnerId).OnDelete(DeleteBehavior.Cascade);

        // Enums as text, like every other enum here: readable in psql and immune to
        // reordering the members.
        builder.Property(preferences => preferences.GridDensity)
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(preferences => preferences.LibrarySort)
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(preferences => preferences.LibraryView)
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(preferences => preferences.VolumeView)
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(preferences => preferences.DefaultLibraryFilter)
            .HasConversion<string>().HasMaxLength(32).IsRequired();
    }
}
