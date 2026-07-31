namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// What a catalogue search turned up for one volume.
/// </summary>
// Carries the hit count as well as the ISBNs so the review file can tell its two very
// different empty answers apart: "this volume is not catalogued at all" is a data
// freshness gap, and "catalogued, but no edition lists an ISBN" is a different problem
// with a different fix.
public sealed record IsbnCandidates
{
    public static IsbnCandidates None { get; } = new() { WorksFound = 0, Isbns = [] };

    public required int WorksFound { get; init; }

    /// <summary>Normalised, de-duplicated, and capped. Every one still needs verifying.</summary>
    public required IReadOnlyList<string> Isbns { get; init; }
}
