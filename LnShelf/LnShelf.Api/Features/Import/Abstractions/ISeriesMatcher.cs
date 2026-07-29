namespace LnShelf.Api.Features.Import.Abstractions;

/// <summary>
/// Scores a series title against a metadata source's candidates and decides
/// whether the winner is clear enough to accept without asking.
/// </summary>
public interface ISeriesMatcher
{
    Task<SeriesMatch> MatchAsync(string seriesTitle, CancellationToken cancellationToken);
}
