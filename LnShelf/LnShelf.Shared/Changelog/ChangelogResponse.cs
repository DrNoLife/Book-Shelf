namespace LnShelf.Shared.Changelog;

/// <summary>
/// The release notes, newest first, alongside the version the running app reports.
/// </summary>
public sealed record ChangelogResponse
{
    /// <summary>The running build's own version — normally <c>Entries[0].Version</c>, but read
    /// from the assembly rather than assumed, so a mismatch is visible instead of masked.</summary>
    public required string CurrentVersion { get; init; }

    public required IReadOnlyList<ChangelogEntryResponse> Entries { get; init; }
}

/// <summary>One released version and what changed in it.</summary>
public sealed record ChangelogEntryResponse
{
    public required string Version { get; init; }
    public required DateOnly Date { get; init; }
    public required string Title { get; init; }

    /// <summary>Something you could not do before.</summary>
    public required IReadOnlyList<string> Features { get; init; }

    /// <summary>An existing thing that now works better, reads better, is faster or safer.</summary>
    public required IReadOnlyList<string> Improvements { get; init; }

    /// <summary>Something that was wrong and now behaves as intended.</summary>
    public required IReadOnlyList<string> Fixes { get; init; }
}
