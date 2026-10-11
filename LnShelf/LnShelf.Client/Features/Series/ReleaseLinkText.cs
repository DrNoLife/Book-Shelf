namespace LnShelf.Client.Features.Series;

/// <summary>
/// The words the "Not linked" panel uses for a candidate and for the outcome of linking.
/// </summary>
public static class ReleaseLinkText
{
    /// <summary>MangaUpdates' page for a record, the one a reader checks before linking.</summary>
    public static string SourcePageUrl(long mangaUpdatesId) =>
        $"https://www.mangaupdates.com/series.html?id={mangaUpdatesId.ToString(CultureInfo.InvariantCulture)}";

    extension(ReleaseLinkCandidateResponse candidate)
    {
        /// <summary>
        /// "Novel · 2015": the two facts a person disambiguating a link asks about first. Empty
        /// when the source gave neither.
        /// </summary>
        public string Detail => string.Join(
            " · ",
            new[] { candidate.SourceType, candidate.StartYear?.ToString(CultureInfo.InvariantCulture) }
                .Where(part => !string.IsNullOrWhiteSpace(part)));

        /// <summary>"Linked to Overlord (manga) on your shelf" — why this one cannot be picked.</summary>
        public string? HeldBy => candidate.LinkedSeriesTitle is { } title
            ? $"Already linked to {title} on your shelf"
            : null;
    }

    /// <summary>What happened, in one line, after a link was saved.</summary>
    public static string Outcome(ReleaseLinkResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.Refreshed
            ? "Linked. The volume list is refreshed from MangaUpdates."
            : "Linked. The volume list follows at the next sync.";
    }
}
