namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// Cuts an upload's text down to what the staging columns hold, before any of it is matched.
/// </summary>
// Done up front because the save is the last step of an upload: a 700-character title in
// row 9,000 used to be a 500 after every AniList search and cover fetch had already been
// spent. Truncated rather than refused — an export with one absurd title is still an export
// worth importing, and the raw title is evidence of the parse, not a key anything joins on.
public static class ImportColumnFitting
{
    extension(ImportedBook book)
    {
        public ImportedBook FittedToColumns() => book with
        {
            RawTitle = book.RawTitle.Truncated(ImportColumnLimits.RawTitle),
            SeriesTitle = book.SeriesTitle.Truncated(ImportColumnLimits.SeriesTitle),
            SeriesKey = book.SeriesKey.Truncated(ImportColumnLimits.SeriesKey),
            Author = book.Author?.Truncated(ImportColumnLimits.Author),
            Subtitle = book.Subtitle?.Truncated(ImportColumnLimits.Subtitle),

            // Dropped rather than cut: half an ISBN is a wrong ISBN, and enrichment would
            // look it up.
            Isbn = book.Isbn is { Length: > ImportColumnLimits.Isbn } ? null : book.Isbn,
        };
    }

    extension(string value)
    {
        public string Truncated(int maximumLength)
        {
            if (value.Length <= maximumLength)
            {
                return value;
            }

            // Never between the two halves of a surrogate pair: a lone half is not valid
            // UTF-8, and Postgres would refuse the whole batch over it.
            var length = char.IsHighSurrogate(value[maximumLength - 1]) ? maximumLength - 1 : maximumLength;

            return value[..length];
        }
    }
}
