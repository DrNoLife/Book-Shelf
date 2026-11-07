namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// Gives a series a synopsis when it has none — the series added before the shelf
/// ever asked AniList for one.
/// </summary>
// Extracted from SeriesRefresher, whose backlog note said the next edit extracts rather than
// adds; the store's next volumes were that edit.
//
// The description only, never the rest of the metadata. Re-applying every AniList field
// here would make a gap-fill indistinguishable from a full re-sync, and the other fields
// were already written when the series was added.
//
// No budget mechanics, deliberately, unlike the volume enrichment. This costs one AniList
// request per description-less series and is self-extinguishing — the guard stops firing
// the moment the column is filled. AniList allows 30 requests a minute and this shelf
// holds single digits of series, so the worst case is one sweep spending nine of them,
// once. A LookupBudget here would be machinery guarding nothing.
//
// Self-extinguishing only because an answer of "no synopsis" is written down too, as the
// empty string — the same three states Volume.Title uses: null is "never asked", empty is
// "asked, and AniList has none". Left null, a series AniList carries no synopsis for would
// be asked about every night forever. The cost is that a synopsis somebody adds to AniList
// later does not arrive on its own; clearing the column to null asks again. Every reader
// treats blank as absent (SeriesDetailProjection's NullIfBlank), so nothing displays it.
//
// The record arrives through a SeriesMetadataLookup rather than the source itself, so a series
// that also lacks its cover costs one request between this and SeriesCoverHealer, not two.
public sealed class SeriesDescriptionHealer(ILogger<SeriesDescriptionHealer> logger)
{
    /// <param name="aniList">The series' AniList record; null when the series has no AniList id.</param>
    public async Task HealAsync(Series series, SeriesMetadataLookup? aniList, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(series);

        if (series.Description is not null || aniList is null)
        {
            return;
        }

        var aniListId = aniList.AniListId;

        try
        {
            var metadata = await aniList.GetAsync(cancellationToken);

            if (metadata?.Description is not { } description)
            {
                // A failure throws and never reaches here, so this is AniList answering — with
                // no synopsis, or with no such series — and the answer is final for this row.
                series.Description = string.Empty;
                return;
            }

            series.Description = description;

            logger.LogInformation(
                "{TitleEnglish}: description filled in from AniList {AniListId}",
                series.TitleEnglish,
                aniListId);
        }
        // The caller's cancellation only: a timed-out request is a cancelled task nobody
        // asked for, and it is exactly the failure this catch exists to absorb.
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // The sync job's failure philosophy, applied one level down: a refresh that got the
            // volume counts right is not worth failing because a synopsis could not be
            // fetched. The next run will try again — the column is still null, so the guard is
            // still true.
            logger.LogWarning(
                exception,
                "Could not fetch a description for {TitleEnglish} (AniList {AniListId}); continuing",
                series.TitleEnglish,
                aniListId);
        }
    }
}
