namespace LnShelf.Api.Features.Metadata.Models;

/// <summary>
/// The works readers of one series also liked, best first.
/// </summary>
/// <param name="ExternalId">The source's own id for the series asked about.</param>
/// <param name="Works">
/// Votes descending and already filtered: at least 2 votes, the same format as the series
/// asked about, nothing adult, not the series itself, de-duplicated.
/// </param>
// Holds no reader's data, so one copy serves every shelf and can be cached for all of them.
public sealed record SeriesRecommendations(int ExternalId, IReadOnlyList<RecommendedWork> Works)
{
    public static SeriesRecommendations None(int externalId) => new(externalId, []);
}

/// <summary>One work a source recommends.</summary>
public sealed record RecommendedWork
{
    public required MetadataSourceName Source { get; init; }

    /// <summary>The source's own id for the work.</summary>
    public required int ExternalId { get; init; }

    public required string Title { get; init; }

    public required Format Format { get; init; }

    /// <summary>The readers' net votes for the pairing.</summary>
    public required int Votes { get; init; }

    /// <summary>The source's cover URL. Never handed to the browser; covers are cached locally.</summary>
    public string? CoverImageUrl { get; init; }
}
