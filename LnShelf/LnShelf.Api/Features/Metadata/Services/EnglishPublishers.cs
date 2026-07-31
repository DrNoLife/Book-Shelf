namespace LnShelf.Api.Features.Metadata.Services;

/// <summary>
/// The English-market light novel and manga publishers, used as the fallback signal
/// when an Open Library record states no language at all.
/// </summary>
// The measured recipe's second clause: "keep the one with languages containing eng,
// or absent that field, a known English-market publisher"
// (.research/volume-enrichment/e-isbn-first-pipeline.md). Plenty of records omit the
// language field entirely, and refusing all of them would throw away real editions.
//
// A list, and therefore incomplete on purpose: it is only ever used to *propose* an
// ISBN into a file a person reviews, never to write one. A publisher missing from it
// costs one manual line in that file, not a wrong shelf entry.
public static class EnglishPublishers
{
    private static readonly string[] Names =
    [
        "Yen Press",
        "Yen On",
        "J-Novel Club",
        "Seven Seas",
        "Airship",
        "Kodansha",
        "Viz Media",
        "Dark Horse",
        "Vertical",
        "Square Enix Manga",
        "One Peace Books",
        "Cross Infinite World",
        "Tokyopop",
        "Sol Press",
        "Hanashi Media",
        "Little, Brown",
        "Hachette",
    ];

    /// <summary>
    /// Whether the publisher name looks like an English-market imprint.
    /// </summary>
    // A contains check rather than equality: the field arrives as "Yen Press LLC",
    // "Kodansha Comics", "VIZ Media LLC" — the imprint is in there with a suffix.
    public static bool Includes(string? publisher)
    {
        if (string.IsNullOrWhiteSpace(publisher))
        {
            return false;
        }

        return Names.Any(name => publisher.Contains(name, StringComparison.OrdinalIgnoreCase));
    }
}
