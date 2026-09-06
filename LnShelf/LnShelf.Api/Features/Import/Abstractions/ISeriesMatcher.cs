namespace LnShelf.Api.Features.Import.Abstractions;

/// <summary>
/// Scores a series title against a metadata source's candidates and decides
/// whether the winner is clear enough to accept without asking.
/// </summary>
public interface ISeriesMatcher
{
    /// <param name="author">
    /// Who the export says wrote the series, when it says. Null otherwise.
    /// </param>
    /// <param name="illustrator">
    /// Who the export says drew it, when it says. Null otherwise — this is the
    /// credit that separates a light novel from its own manga adaptation, so it
    /// is the one worth carrying through.
    /// </param>
    Task<SeriesMatch> MatchAsync(
        string seriesTitle,
        string? author,
        string? illustrator,
        CancellationToken cancellationToken);
}
