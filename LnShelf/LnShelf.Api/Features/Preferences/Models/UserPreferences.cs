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

    /// <summary>Covers is the default — the design's thesis is covers doing the talking.</summary>
    public LibraryView LibraryView { get; set; } = LibraryView.Covers;

    /// <summary>The series-detail default; a series may override it.</summary>
    public VolumeView VolumeView { get; set; } = VolumeView.Tiles;

    /// <summary>
    /// Whether a volume's own title is printed in its caption where one exists.
    /// </summary>
    // Off by default, and deliberately: light-novel lines mostly name their volumes
    // "Volume N", so on most of the shelf this adds a second copy of the number the
    // caption already prints. It earns its place on the series where the publisher gave
    // the volumes real names, which is the user's judgement to make and not a default's.
    public bool ShowVolumeTitles { get; set; }

    /// <summary>Behind is the opinionated default (§4.6).</summary>
    public LibraryFilter DefaultLibraryFilter { get; set; } = LibraryFilter.Behind;
}
