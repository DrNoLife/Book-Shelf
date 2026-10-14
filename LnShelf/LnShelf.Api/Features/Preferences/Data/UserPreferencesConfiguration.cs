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

        // A database default, so the release before this column existed can still insert a row
        // (migrations stay backward compatible for one release).
        builder.Property(preferences => preferences.DefaultProgressUnit)
            .HasConversion<string>().HasMaxLength(32).IsRequired().HasDefaultValue(ProgressUnit.Volumes);

        // The two ordered sets as comma-separated names in one text column each, rather than a
        // child table or a Postgres array: a handful of values read and written only as a
        // whole, and names keep them as readable in psql as the single enums above.
        builder.Property(preferences => preferences.DefaultLibraryFilters)
            .HasConversion(new EnumListConverter<LibraryFilter>(), new EnumListComparer<LibraryFilter>())
            .HasMaxLength(256).IsRequired();
        builder.Property(preferences => preferences.LibrarySectionOrder)
            .HasConversion(new EnumListConverter<LibrarySection>(), new EnumListComparer<LibrarySection>())
            .HasMaxLength(128).IsRequired();
    }
}
