namespace LnShelf.Shared.Import;

/// <summary>
/// Why a group is in the review queue. The queue prints this, because "review
/// this" without saying what is uncertain is not a question the user can answer.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<ImportReviewReason>))]
public enum ImportReviewReason
{
    None,

    /// <summary>
    /// The best candidates are a light novel and its manga adaptation, titled
    /// identically. Only the reader knows which one they read — this is the
    /// common case for a StoryGraph export, and it is never guessed.
    /// </summary>
    FormatAmbiguous,

    /// <summary>Two candidates scored too close to separate on title alone.</summary>
    AmbiguousCandidates,

    /// <summary>The best candidate was not close enough to accept unseen.</summary>
    LowConfidence,

    /// <summary>The source returned nothing worth showing.</summary>
    NoCandidate,
}
