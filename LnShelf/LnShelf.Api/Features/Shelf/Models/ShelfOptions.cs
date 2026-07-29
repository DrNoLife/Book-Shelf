namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>
/// Shelf-level configuration.
/// </summary>
public sealed class ShelfOptions
{
    public const string SectionName = "Shelf";

    /// <summary>
    /// Whether to seed <c>docs/seed-library.json</c> into an empty shelf.
    /// </summary>
    // Off by default, which is a change of default: the 24-series fixture existed
    // so the grid had something to render before any real data existed, and phase
    // 4 replaced it with an actual imported library. Leaving it on would put a
    // second "Overlord" and a second "Mushoku Tensei" — with invented counts — next
    // to the real ones. Set it to true on a fresh machine to get the fixture back.
    public bool SeedDevelopmentFixture { get; set; }
}
