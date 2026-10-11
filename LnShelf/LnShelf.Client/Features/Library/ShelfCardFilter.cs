namespace LnShelf.Client.Features.Library;

/// <summary>
/// The shelf's two in-browser filters: the text box (title, romaji title or author) and the
/// genre chips (any of them). The status and format filters are the server's
/// (LibraryCardFilters); these narrow what it sent, without a round trip.
/// </summary>
public static class ShelfCardFilter
{
    /// <summary>
    /// Matches the romaji title too, so typing "Yakumo" finds a series whose English title is
    /// something else entirely — and the author, so "Carlo Zen" finds everything by him.
    /// </summary>
    public static bool MatchesText(LibraryCardResponse card, string needle)
    {
        ArgumentNullException.ThrowIfNull(card);

        return card.TitleEnglish.Contains(needle, StringComparison.OrdinalIgnoreCase)
               || (card.TitleRomaji?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
               || (card.Author?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    /// <summary>Any of the chosen genres, the way the status chips are any-of. None chosen is no filter.</summary>
    public static bool HasAnyGenre(LibraryCardResponse card, IReadOnlyCollection<string> genres)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(genres);

        return genres.Count == 0
               || card.Genres.Any(genre => genres.Contains(genre, StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The chips the filter panel offers: every genre on these cards, plus any chosen one no
    /// card carries any more (so a chip that is on can always be turned off), once each, A–Z.
    /// </summary>
    public static IReadOnlyList<string> GenreChoices(
        IEnumerable<LibraryCardResponse> cards,
        IEnumerable<string> selected) =>
        [
            .. cards
                .SelectMany(card => card.Genres)
                .Concat(selected)
                .Where(genre => !string.IsNullOrWhiteSpace(genre))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase),
        ];

    /// <summary>A tap on a genre chip: on if it was off, off if it was on.</summary>
    public static IReadOnlyList<string> Toggle(IReadOnlyList<string> selected, string genre)
    {
        ArgumentNullException.ThrowIfNull(selected);

        return selected.Contains(genre, StringComparer.OrdinalIgnoreCase)
            ? [.. selected.Where(chosen => !string.Equals(chosen, genre, StringComparison.OrdinalIgnoreCase))]
            : [.. selected, genre];
    }
}
