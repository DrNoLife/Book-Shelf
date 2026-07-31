namespace LnShelf.Api.Features.Metadata.Mappers;

/// <summary>
/// Clean discovery row ↔ the review file's own row shape.
/// </summary>
public static class IsbnReviewRowMapper
{
    extension(IsbnDiscoveryRow row)
    {
        public IsbnReviewCsvRow ToCsvRow() => new()
        {
            SeriesId = row.SeriesId.ToString(),
            Series = row.SeriesTitle,
            Volume = row.VolumeNumber,
            Isbn = row.Isbn,
            OpenLibraryTitle = row.OpenLibraryTitle,
            Publisher = row.Publisher,
            Outcome = row.Outcome.ToString(),
            Note = row.Note,
        };
    }

    extension(IsbnReviewCsvRow csvRow)
    {
        /// <summary>
        /// The half of the row the apply command acts on: which volume, which ISBN.
        /// </summary>
        // The other columns are evidence for the person reviewing, not instructions.
        // Reading them back would let a hand-edited title silently become shelf data.
        public IsbnReviewInstruction? ToInstruction()
        {
            ArgumentNullException.ThrowIfNull(csvRow);

            var isbn = Isbn.Normalize(csvRow.Isbn);

            if (isbn is null || !Guid.TryParse(csvRow.SeriesId, out var seriesId) || csvRow.Volume <= 0)
            {
                return null;
            }

            return new IsbnReviewInstruction
            {
                SeriesId = seriesId,
                VolumeNumber = csvRow.Volume,
                Isbn = isbn,
            };
        }
    }
}
