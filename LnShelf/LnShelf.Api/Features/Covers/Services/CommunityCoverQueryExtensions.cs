namespace LnShelf.Api.Features.Covers.Services;

public static class CommunityCoverQueryExtensions
{
    extension(IQueryable<CommunityCover> source)
    {
        /// <summary>
        /// The rows published for this volume by anyone but <paramref name="userId"/>: the same
        /// format and number, and the same AniList or MangaUpdates id.
        /// </summary>
        // A series with neither id matches nothing, so a manual series never gets candidates.
        public IQueryable<CommunityCover> MatchingOthers(Series series, Volume volume, Guid userId)
        {
            ArgumentNullException.ThrowIfNull(series);
            ArgumentNullException.ThrowIfNull(volume);

            var aniListId = series.AniListId;
            var mangaUpdatesId = series.MangaUpdatesId;
            var format = series.Format;
            var number = volume.Number;

            if (aniListId is null && mangaUpdatesId is null)
            {
                return source.Where(_ => false);
            }

            return source.Where(cover =>
                cover.UploaderUserId != userId
                && cover.Format == format
                && cover.Number == number
                && ((aniListId != null && cover.AniListId == aniListId)
                    || (mangaUpdatesId != null && cover.MangaUpdatesId == mangaUpdatesId)));
        }
    }
}
