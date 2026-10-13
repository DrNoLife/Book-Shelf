namespace LnShelf.Api.Features.Recommendations.Models;

/// <summary>
/// One work the user hid from For you with "Not interested".
/// </summary>
// A root of its own (IOwned) rather than a child of Series: a dismissed work is by definition
// not on the shelf, so there is no series to hang it on.
public sealed class RecommendationDismissal : IOwned
{
    /// <summary>The title's width; equal to Series.TitleEnglish's.</summary>
    public const int TitleMaximumLength = 400;

    public Guid Id { get; set; }

    /// <summary>The user this belongs to (IOwned). Stamped on save.</summary>
    public Guid OwnerId { get; set; }

    /// <summary>The catalogue's id for the hidden work.</summary>
    public int AniListId { get; set; }

    /// <summary>What the hidden list calls the work, so it can be shown without asking the catalogue.</summary>
    public string Title { get; set; } = string.Empty;

    public Format Format { get; set; }

    public DateTimeOffset DismissedAt { get; set; }
}
