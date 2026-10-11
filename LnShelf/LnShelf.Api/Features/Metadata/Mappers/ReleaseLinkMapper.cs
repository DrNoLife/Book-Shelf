namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>A release-source search hit → the row a person picks a link from.</summary>
public static class ReleaseLinkMapper
{
    /// <param name="linkedElsewhere">The other series on the shelf already holding this record, if any.</param>
    public static ReleaseLinkCandidateResponse ToResponse(
        ReleaseSeriesCandidate candidate,
        LinkedSeries? linkedElsewhere)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return new ReleaseLinkCandidateResponse
        {
            MangaUpdatesId = candidate.ExternalId,
            Title = candidate.Title,
            SourceType = candidate.SourceType,
            StartYear = candidate.StartYear,
            LinkedSeriesId = linkedElsewhere?.SeriesId,
            LinkedSeriesTitle = linkedElsewhere?.Title,
        };
    }
}
