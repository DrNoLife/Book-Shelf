namespace LnShelf.Client.Features.Library;

/// <summary>
/// Links into the shelf with one filter applied: a genre tag (<c>/?genre=Fantasy</c>) or an
/// author (<c>/?q=Carlo Zen</c>, the title filter, which matches authors too). The library
/// reads them once on arrival (ShelfSection) and they are not remembered.
/// </summary>
public static class LibraryLinks
{
    /// <summary>The query-string name a genre arrives under.</summary>
    public const string GenreParameter = "genre";

    /// <summary>The query-string name the title-or-author filter arrives under.</summary>
    public const string TextParameter = "q";

    public static string ForGenre(string genre) =>
        $"/?{GenreParameter}={Uri.EscapeDataString(genre)}";

    public static string ForText(string text) =>
        $"/?{TextParameter}={Uri.EscapeDataString(text)}";

    /// <summary>
    /// The genre and the text a library URL carries; null for each it does not. Only the
    /// library itself (the root path) carries them: a <c>?q=</c> on any other page is that
    /// page's business, not a filter for the shelf.
    /// </summary>
    // Read by hand rather than through [SupplyParameterFromQuery]: the shelf section that applies
    // them is not the routable page, and the query is two plain names. The path check matters
    // because the section hears every navigation, including the one that leaves the library.
    public static LibraryLink Read(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || parsed.AbsolutePath != "/")
        {
            return LibraryLink.None;
        }

        var queryStart = uri.IndexOf('?', StringComparison.Ordinal);
        if (queryStart < 0)
        {
            return LibraryLink.None;
        }

        var fragmentStart = uri.IndexOf('#', queryStart);
        var query = fragmentStart < 0 ? uri[(queryStart + 1)..] : uri[(queryStart + 1)..fragmentStart];

        string? genre = null;
        string? text = null;

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = pair[..separator];

            // A form-encoded space arrives as '+'; UnescapeDataString leaves that alone.
            var value = Uri.UnescapeDataString(pair[(separator + 1)..].Replace('+', ' ')).Trim();
            if (value.Length == 0)
            {
                continue;
            }

            if (name == GenreParameter)
            {
                genre = value;
            }
            else if (name == TextParameter)
            {
                text = value;
            }
        }

        return new LibraryLink(genre, text);
    }
}

/// <summary>What a link into the shelf asks it to show. Both null is a plain visit.</summary>
public sealed record LibraryLink(string? Genre, string? Text)
{
    public static readonly LibraryLink None = new(Genre: null, Text: null);

    public bool IsEmpty => Genre is null && Text is null;
}
