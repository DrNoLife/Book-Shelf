namespace LnShelf.Api.Features.Shelf.Data;

/// <summary>
/// Serialization shape of <c>docs/seed-library.json</c>.
/// </summary>
// A dirty boundary model, so: classes, permissive nullability, strings where the
// file has strings. It is mapped to entities in ShelfSeedData and never travels
// further inward (CLAUDE.md — Types: records vs classes).
public sealed class SeedLibraryDocument
{
    public string? Comment { get; set; }
    public List<SeedSeries> Series { get; set; } = [];
}

/// <inheritdoc cref="SeedLibraryDocument"/>
public sealed class SeedSeries
{
    public string TitleEnglish { get; set; } = string.Empty;
    public string? TitleRomaji { get; set; }
    public string? TitleNative { get; set; }

    /// <summary>Parsed to <see cref="Shared.Shelf.Format"/>.</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>Publication status, parsed to <see cref="PublicationStatus"/>.</summary>
    public string Status { get; set; } = string.Empty;

    public int Read { get; set; }
    public int Owned { get; set; }
    public int Published { get; set; }
    public bool NewVolume { get; set; }

    /// <summary>
    /// Free-text volume breakdown. Present only for the series that has a
    /// hand-built builder; its presence means "do not generate from counts".
    /// </summary>
    public string? Volumes { get; set; }

    public string? Notes { get; set; }
    public string? Author { get; set; }
    public string? Illustrator { get; set; }
    public string? Publisher { get; set; }
    public int? StartYear { get; set; }
    public List<string>? Genres { get; set; }
}
