using LnShelf.Api.Errors;

namespace LnShelf.Api.Features.Shelf.Services;

/// <summary>
/// Which source an add request names — and the refusal when it names none or both.
/// </summary>
// Static and public so the rule can be asserted directly, the way the verification
// thresholds are: it is the whole of the 400 on POST /api/series, and a rule that can only
// be exercised by starting the app is a rule nobody can change with confidence.
//
// A rule rather than a `required` field on the request, because "exactly one of two" is not
// something a record can state. Guessing is not on the table: the two ids are different
// catalogues' names, and the same shelf holds series linked to one, the other or both.
public static class AddSeriesSource
{
    /// <exception cref="InvalidInputException">Neither id was given, or both were.</exception>
    public static MetadataSourceName Of(AddSeriesRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return (request.AniListId, request.MangaUpdatesId) switch
        {
            (not null, null) => MetadataSourceName.AniList,
            (null, not null) => MetadataSourceName.MangaUpdates,
            _ => throw new InvalidInputException(
                "Adding a series needs exactly one of aniListId or mangaUpdatesId — "
                + "not both, and not neither."),
        };
    }
}
