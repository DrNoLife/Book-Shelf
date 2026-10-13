namespace LnShelf.Api.Features.Recommendations.Models;

/// <summary>
/// A shelf series that For you draws suggestions from.
/// </summary>
/// <param name="Weight">How much the reader's taste leans on it; below zero marks a Dropped series.</param>
public sealed record RecommendationSeed(Guid SeriesId, int ExternalId, string Title, Format Format, double Weight);
