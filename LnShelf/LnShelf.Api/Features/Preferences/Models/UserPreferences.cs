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
    // On by default since the catalogue pass: titles used to be a promise the shelf could
    // not keep (no source named a volume), so the default hid an empty feature. They are
    // now real data on the large majority of the shelf, and a default that hides data the
    // app went and fetched serves nobody. The toggle stays for the reader who finds
    // "Volume 3" after "Vol 3" redundant, and a series with no titles still shows none.
    public bool ShowVolumeTitles { get; set; } = true;

    /// <summary>Behind is the opinionated default (§4.6).</summary>
    public LibraryFilter DefaultLibraryFilter { get; set; } = LibraryFilter.Behind;
}
