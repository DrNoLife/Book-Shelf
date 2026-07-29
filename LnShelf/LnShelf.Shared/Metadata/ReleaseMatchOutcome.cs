namespace LnShelf.Shared.Metadata;

/// <summary>
/// What happened when the shelf tried to link a series to the release source.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReleaseMatchOutcome>))]
public enum ReleaseMatchOutcome
{
    /// <summary>One candidate won clearly; the link was written.</summary>
    Linked,

    /// <summary>The source returned nothing for this title.</summary>
    NoCandidate,

    /// <summary>The best candidate was not close enough to accept without asking.</summary>
    LowConfidence,

    /// <summary>Two candidates were too close to separate.</summary>
    Ambiguous,

    /// <summary>
    /// The best candidate is already linked to a different series on the shelf.
    /// </summary>
    // The real case: MangaUpdates files Ascendance of a Bookworm as one series of 33
    // volumes, while AniList splits it into a series per part. Two shelf entries
    // therefore have one correct answer between them, which is not a link either of
    // them can have.
    AlreadyLinked,
}
