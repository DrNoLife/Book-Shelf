namespace LnShelf.Client;

/// <summary>
/// The "Faraos Cigarer" link: a search on Faraos' webshop for the series, built client-side
/// like <see cref="SaxoSearch"/>.
/// </summary>
// The series, not the volume: Faraos' search finds nothing once a volume number is in the
// query ("Overlord 13", "Dragon Ball 15" both come back empty although both are stocked),
// so the link lands on the series' list and the reader picks the volume there. A light
// novel gets " Ln", which is how Faraos names those editions ("Overlord Ln vol. 13"), so
// the manga does not crowd the list.
public static class FaraosSearch
{
    public static string UrlFor(string title, Format format) =>
        "https://www.faraos.dk/filtrering?search="
        + Uri.EscapeDataString(format == Format.LightNovel ? $"{title} Ln" : title);
}
