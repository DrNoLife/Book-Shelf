namespace LnShelf.Api.Features.Preferences.Models;

/// <summary>
/// The single preferences row (feature-spec §4.6).
/// </summary>
// One user, one row, and a fixed key rather than a generated one: "the settings"
// is a singleton, and a table that could hold two of them would invite code that
// has to choose between them.
public sealed class UserPreferences
{
    /// <summary>The only id this table ever has.</summary>
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;

    public GridDensity GridDensity { get; set; } = GridDensity.Comfortable;

    public LibrarySort LibrarySort { get; set; } = LibrarySort.MostBehind;

    /// <summary>Behind is the opinionated default (§4.6).</summary>
    public LibraryFilter DefaultLibraryFilter { get; set; } = LibraryFilter.Behind;
}
