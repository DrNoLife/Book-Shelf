namespace LnShelf.Api.Features.Shelf.Models;

/// <summary>
/// Move the user state of volume <see cref="FromNumber"/> onto <see cref="IntoNumber"/> of one
/// series, then delete <see cref="FromNumber"/>.
/// </summary>
/// <param name="SeriesTitle">The exact English or romaji title.</param>
/// <param name="Format">Narrows the title to one format, for a title the LN and manga share.</param>
/// <param name="FromNumber">The volume merged away and deleted.</param>
/// <param name="IntoNumber">The volume that receives the user state.</param>
/// <param name="DryRun">Reports what would happen and writes nothing.</param>
public sealed record VolumeMergeRequest(
    string SeriesTitle,
    Format? Format,
    decimal FromNumber,
    decimal IntoNumber,
    bool DryRun);
