namespace LnShelf.Api.Features.Preferences.Data;

public sealed class UserPreferencesConfiguration : IEntityTypeConfiguration<UserPreferences>
{
    public void Configure(EntityTypeBuilder<UserPreferences> builder)
    {
        builder.ToTable("user_preferences");
        builder.HasKey(preferences => preferences.Id);

        // Assigned by the application — and always the same value.
        builder.Property(preferences => preferences.Id).ValueGeneratedNever();

        // Enums as text, like every other enum here: readable in psql and immune to
        // reordering the members.
        builder.Property(preferences => preferences.GridDensity)
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(preferences => preferences.LibrarySort)
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(preferences => preferences.DefaultLibraryFilter)
            .HasConversion<string>().HasMaxLength(32).IsRequired();
    }
}
