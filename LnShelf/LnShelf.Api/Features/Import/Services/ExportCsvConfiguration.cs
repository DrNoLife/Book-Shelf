using CsvHelper;

namespace LnShelf.Api.Features.Import.Services;

/// <summary>
/// How every export CSV is parsed, whoever wrote it.
/// </summary>
// Forgiving on purpose. These files are exported by someone else's product: a
// column that moved, a header that gained a word, or a row with a ragged field
// count must not throw away the other 75 rows. What it must never do is silently
// invent data, which is why every skipped row is logged by the reader.
//
// Shared by both readers rather than copied into each, because it is one policy
// about other people's files — a second copy would be a second answer to the
// same question.
internal static class ExportCsvConfiguration
{
    public static readonly CsvReaderOptions Forgiving = new()
    {
        // Pinned rather than detected: the default sniffs comma, semicolon, tab or bar from
        // the start of the input, and a title row full of semicolons must not change the answer.
        Separator = ',',

        // The StoryGraph export really does contain "オーバーロード1 不死者の王 " — a
        // trailing space inside the quotes.
        TrimWhitespace = true,
        TrimInsideQuotes = true,
    };
}
